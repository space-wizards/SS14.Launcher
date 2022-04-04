using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Dapper;
using Microsoft.Data.Sqlite;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using SharpZstd.Interop;
using Splat;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Utility;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SS14.Launcher.Models;

public sealed class Updater : ReactiveObject
{
    private const int ManifestDownloadProtocolVersion = 1;

    private static readonly IDeserializer ResourceManifestDeserializer =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    private readonly DataManager _cfg;
    private readonly IEngineManager _engineManager;
    private readonly HttpClient _http;
    private bool _updating;

    public Updater()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    // Note: these get updated from different threads. Observe responsibly.
    [Reactive] public UpdateStatus Status { get; private set; }
    [Reactive] public (long downloaded, long total, ProgressUnit unit)? Progress { get; private set; }

    public async Task<ContentLaunchInfo?> RunUpdateForLaunchAsync(
        ServerBuildInformation buildInformation,
        CancellationToken cancel = default)
    {
        if (_updating)
        {
            throw new InvalidOperationException("Update already in progress.");
        }

        _updating = true;

        try
        {
            var launchInfo = await RunUpdate(buildInformation, cancel);
            Status = UpdateStatus.Ready;
            return launchInfo;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Status = UpdateStatus.Error;
            Log.Error(e, "Exception while trying to run updates");
        }
        finally
        {
            _updating = false;
        }

        return null;
    }

    private async Task<ContentLaunchInfo> RunUpdate(
        ServerBuildInformation buildInfo,
        CancellationToken cancel)
    {
        Status = UpdateStatus.CheckingClientUpdate;

        // Both content downloading and engine downloading MAY need the manifest.
        // So use a Lazy<Task<T>> to avoid loading it twice.
        var moduleManifest =
            new Lazy<Task<EngineModuleManifest>>(() => _engineManager.GetEngineModuleManifest(cancel));

        // ReSharper disable once UseAwaitUsing
        using var con = ContentManager.GetSqliteConnection();
        var versionRowId = await Task.Run(
            () => TouchOrDownloadContentUpdate(buildInfo, con, moduleManifest, cancel),
            CancellationToken.None);

        Log.Information("Checking to cull old content versions...");

        await Task.Run(() => { CullOldContentVersions(con); }, CancellationToken.None);

        (string, string)[] modules;

        {
            Status = UpdateStatus.CheckingClientUpdate;
            modules = con.Query<(string, string)>(
                "SELECT ModuleName, moduleVersion FROM ContentEngineDependency WHERE VersionId = @Version",
                new { Version = versionRowId }).ToArray();

            foreach (var (name, version) in modules)
            {
                if (name == "Robust")
                {
                    await InstallEngineVersionIfMissing(version, cancel);
                }
                else
                {
                    Status = UpdateStatus.DownloadingEngineModules;

                    var manifest = await moduleManifest.Value;
                    await _engineManager.DownloadModuleIfNecessary(
                        name,
                        version,
                        manifest,
                        DownloadProgressCallback,
                        cancel);
                }
            }
        }

        Status = UpdateStatus.CullingEngine;
        await CullEngineVersionsMaybe(con);

        Status = UpdateStatus.CommittingDownload;
        _cfg.CommitConfig();

        Log.Information("Update done!");
        return new ContentLaunchInfo(versionRowId, modules);
    }

    private void CullOldContentVersions(SqliteConnection con)
    {
        using var tx = con.BeginTransaction();

        Status = UpdateStatus.CullingContent;

        // We keep at most MaxVersionsToKeep TOTAL.
        // We keep at most MaxForkVersionsToKeep of a specific ForkID.
        // Old builds get culled first.

        var maxVersions = _cfg.GetCVar(CVars.MaxVersionsToKeep);
        var maxForkVersions = _cfg.GetCVar(CVars.MaxForkVersionsToKeep);

        var versions = con.Query<ContentVersion>("SELECT * FROM ContentVersion ORDER BY LastUsed DESC").ToArray();

        var forkCounts = versions.Select(x => x.ForkId).Distinct().ToDictionary(x => x, _ => 0);

        var totalCount = 0;
        foreach (var version in versions)
        {
            ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(forkCounts, version.ForkId, out _);

            var keep = count < maxForkVersions && totalCount < maxVersions;
            if (keep)
            {
                count += 1;
                totalCount += 1;
            }
            else
            {
                Log.Debug("Culling version {ForkId}/{ForkVersion}", version.ForkId, version.ForkVersion);
                con.Execute("DELETE FROM ContentVersion WHERE Id = @Id", new { version.Id });
            }
        }

        if (totalCount != versions.Length)
        {
            var rows = con.Execute("DELETE FROM Content WHERE Id NOT IN (SELECT ContentId FROM ContentManifest)");
            Log.Debug("Culled {RowsCulled} orphaned content blobs", rows);
        }

        tx.Commit();
    }

    private static ContentVersion? CheckExisting(SqliteConnection con, ServerBuildInformation buildInfo)
    {
        // Check if we already have this version installed in the content DB.

        Log.Debug(
            "Checking to see if we already have version for fork {ForkId}/{ForkVersion} ZipHash: {ZipHash} ManifestHash: {ManifestHash}",
            buildInfo.ForkId, buildInfo.Version, buildInfo.Hash, buildInfo.ManifestHash);

        ContentVersion? found;
        if (buildInfo.ManifestHash is { } manifestHashHex)
        {
            // Manifest hash is ultimate source of truth.
            var hash = Convert.FromHexString(manifestHashHex);

            found = con.QueryFirstOrDefault<ContentVersion>(
                "SELECT * FROM ContentVersion WHERE Hash = @Hash", new { Hash = hash });
        }
        else if (buildInfo.Hash is { } hashHex)
        {
            // If the server ONLY provides a zip hash, look up purely by it.
            var hash = Convert.FromHexString(hashHex);

            found = con.QueryFirstOrDefault<ContentVersion>(
                "SELECT * FROM ContentVersion WHERE ZipHash = @ZipHash", new { ZipHash = hash });
        }
        else
        {
            // If no hash, just use forkID/Version and hope for the best.
            // Why do I even support this?
            // Testing I guess?

            found = con.QueryFirstOrDefault<ContentVersion>(
                "SELECT * FROM ContentVersion WHERE ForkId = @ForkId AND ForkVersion = @Version",
                new { buildInfo.ForkId, buildInfo.Version });
        }


        if (found == null)
        {
            Log.Debug("Did not find matching version");
            return null;
        }
        else
        {
            Log.Debug("Found matching version: {Version}", found.Id);
            return found;
        }
    }

    private async Task<long> TouchOrDownloadContentUpdate(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        Lazy<Task<EngineModuleManifest>> moduleManifest,
        CancellationToken cancel)
    {
        // ReSharper disable once UseAwaitUsing
        using var transaction = con.BeginTransaction();

        // Check if we already have this version KNOWN GOOD installed in the content DB.
        var existingVersion = CheckExisting(con, buildInfo);

        long versionId;
        var engineVersion = buildInfo.EngineVersion;
        if (existingVersion == null)
        {
            versionId = await DownloadNewVersion(buildInfo, con, moduleManifest, cancel, engineVersion);
        }
        else
        {
            versionId = await DuplicateExistingVersion(buildInfo, con, moduleManifest, existingVersion, engineVersion);
        }

        Status = UpdateStatus.CommittingDownload;

        transaction.Commit();

        return versionId;
    }

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    private static async Task<long> DuplicateExistingVersion(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        Lazy<Task<EngineModuleManifest>> moduleManifest,
        ContentVersion existingVersion,
        string engineVersion)
    {
        long versionId;

        // If version info does not match server-provided info exactly,
        // we have to create a clone with the different data.
        // This can happen if the server, for some reason,
        // reports a different ForkID/version/engine version for a zip file we already have.

        var curEngineVersion =
            con.ExecuteScalar<string>(
                "SELECT ModuleVersion FROM ContentEngineDependency WHERE ModuleName = 'Robust' AND VersionId = @Version",
                new { Version = existingVersion.Id });

        var changedFork = buildInfo.ForkId != existingVersion.ForkId ||
                          buildInfo.Version != existingVersion.ForkVersion;
        var changedEngineVersion = engineVersion != curEngineVersion;

        if (changedFork || changedEngineVersion)
        {
            versionId = con.ExecuteScalar<long>(
                @"INSERT INTO ContentVersion (Hash, ForkId, ForkVersion, LastUsed, ZipHash)
                    VALUES (@Hash, @ForkId, @ForkVersion, datetime('now'), @ZipHash)
                    RETURNING Id", new
                {
                    existingVersion.Hash,
                    buildInfo.ForkId,
                    ForkVersion = buildInfo.Version,
                    existingVersion.ZipHash
                });

            // Copy entire manifest over.
            con.Execute(@"
                    INSERT INTO ContentManifest (VersionId, Path, ContentId)
                    SELECT (@NewVersion, Path, ContentId)
                    FROM ContentManifest
                    WHERE VersionId = @OldVersion",
                new
                {
                    NewVersion = versionId,
                    OldVersion = existingVersion.ForkVersion
                });

            if (changedEngineVersion)
            {
                con.Execute(@"
                        INSERT INTO ContentEngineDependency (VersionId, ModuleName, ModuleVersion)
                        VALUES (@VersionId, 'Robust', @EngineVersion)",
                    new { EngineVersion = engineVersion });

                // Recalculate module dependencies.
                var oldDependencies = con.Query<string>(@"
                        SELECT ModuleName
                        FROM ContentEngineDependency
                        WHERE VersionId = @OldVersion AND ModuleName != 'Robust'").ToArray();

                if (oldDependencies.Length > 0)
                {
                    var manifest = await moduleManifest.Value;

                    foreach (var module in oldDependencies)
                    {
                        var version = IEngineManager.ResolveEngineModuleVersion(manifest, module, engineVersion);

                        con.Execute(@"
                                INSERT INTO ContentEngineDependency(VersionId, ModuleName, ModuleVersion)
                                VALUES (@Version, @ModName, @EngineVersion)",
                            new
                            {
                                Version = versionId,
                                ModName = module,
                                ModVersion = version
                            });
                    }
                }
            }
            else
            {
                // Copy module dependencies.
                con.Execute(@"
                    INSERT INTO ContentEngineDependency (VersionId, ModuleName, ModuleVersion)
                    SELECT (@NewVersion, ModuleName, ModuleVersion)
                    FROM ContentEngineDependency
                    WHERE VersionId = @OldVersion",
                    new
                    {
                        NewVersion = versionId,
                        OldVersion = existingVersion.ForkVersion
                    });
            }
        }
        else
        {
            versionId = existingVersion.Id;
            // If we do have an exact match we are not changing anything, *except* the LastUsed column.
            con.Execute("UPDATE ContentVersion SET LastUsed = datetime('now') WHERE Id = @Version",
                new { Version = existingVersion.Id });
        }

        return versionId;
    }

    /// <returns>The manifest hash</returns>
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverloadWithCancellation")]
    [SuppressMessage("ReSharper", "UseAwaitUsing")]
    private async Task<long> DownloadNewVersion(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        Lazy<Task<EngineModuleManifest>> moduleManifest,
        CancellationToken cancel,
        string engineVersion)
    {
        // Don't have this version, download it.

        var versionId = con.ExecuteScalar<long>(
            @"INSERT INTO ContentVersion(Hash, ForkId, ForkVersion, LastUsed, ZipHash)
                VALUES (zeroblob(32), @ForkId, @Version, datetime('now'), NULL)
                RETURNING Id",
            new
            {
                buildInfo.ForkId,
                buildInfo.Version
            });

        // TODO: Download URL
        byte[] manifestHash;
        if (!string.IsNullOrEmpty(buildInfo.ManifestUrl)
            && !string.IsNullOrEmpty(buildInfo.ManifestDownloadUrl)
            && !string.IsNullOrEmpty(buildInfo.ManifestHash))
        {
            manifestHash = await DownloadNewVersionManifest(buildInfo, con, versionId, cancel);
        }
        else
        {
            manifestHash = await DownloadNewVersionZip(buildInfo, con, versionId, cancel);
        }

        Log.Debug("Manifest hash: {ManifestHash}", Convert.ToHexString(manifestHash));

        con.Execute(
            "UPDATE ContentVersion SET Hash = @Hash WHERE Id = @Id",
            new { Hash = manifestHash, Id = versionId });

        // Insert engine dependencies.

        // Engine version.
        con.Execute(
            @"INSERT INTO ContentEngineDependency(VersionId, ModuleName, ModuleVersion)
                VALUES (@Version, 'Robust', @EngineVersion)",
            new
            {
                Version = versionId, EngineVersion = engineVersion
            });

        Log.Debug("Inserting dependency: {ModuleName} {ModuleVersion}", "Robust", engineVersion);

        // If we have a manifest file, load module dependencies from manifest file.
        if (ContentManager.OpenBlob(con, versionId, "manifest.yml") is { } resourceManifest)
        {
            string[] modules;
            using (resourceManifest)
            {
                modules = GetModuleNames(resourceManifest);
            }

            if (modules.Length > 0)
            {
                var manifest = await moduleManifest.Value;

                foreach (var module in modules)
                {
                    var version = IEngineManager.ResolveEngineModuleVersion(manifest, module, engineVersion);

                    con.Execute(
                        @"INSERT INTO ContentEngineDependency(VersionId, ModuleName, ModuleVersion)
                        VALUES (@Version, @ModName, @EngineVersion)",
                        new
                        {
                            Version = versionId,
                            ModName = module,
                            ModVersion = version
                        });

                    Log.Debug("Inserting dependency: {ModuleName} {ModuleVersion}", module, version);
                }
            }
        }

        return versionId;
    }

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverloadWithCancellation")]
    [SuppressMessage("ReSharper", "UseAwaitUsing")]
    private async Task<byte[]> DownloadNewVersionManifest(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        long versionId,
        CancellationToken cancel)
    {
        // Download manifest first.

        var manifest = await _http.GetByteArrayAsync(buildInfo.ManifestUrl, cancel);
        var manifestHash = SHA256.HashData(manifest);

        if (Convert.ToHexString(manifestHash) != buildInfo.ManifestHash)
            throw new UpdateException("Manifest has incorrect hash!");

        // Go over the manifest, reading it into the SQLite ContentManifest table.
        // For any content blobs we don't have yet, we put a placeholder entry in the database for now.
        // Keep track of all files we need to download for later.

        var sr = new StreamReader(new MemoryStream(manifest));

        if (sr.ReadLine() != "Robust Content Manifest 1")
            throw new UpdateException("Unknown manifest header!");

        var toDownload = new List<(long rowid, int index)>();

        var lineIndex = 0;
        while (sr.ReadLine() is { } manifestLine)
        {
            cancel.ThrowIfCancellationRequested();

            var sep = manifestLine.IndexOf(' ');
            var hash = Convert.FromHexString(manifestLine.AsSpan(0, sep));
            var filename = manifestLine[(sep + 1)..];

            var row = con.QueryFirstOrDefault<long>(
                "SELECT Id FROM Content WHERE Hash = @Hash",
                new { Hash = hash });
            if (row == 0)
            {
                // Insert placeholder
                row = con.ExecuteScalar<long>(
                    "INSERT INTO Content (Hash, Size, Compression, Data) VALUES (@Hash, 0, 0, zeroblob(0)) RETURNING Id",
                    new { Hash = hash });

                toDownload.Add((row, lineIndex));
            }

            con.Execute(
                "INSERT INTO ContentManifest(VersionId, Path, ContentId) VALUES (@VersionId, @Path, @ContentId)",
                new
                {
                    VersionId = versionId,
                    Path = filename,
                    ContentId = row,
                });

            lineIndex += 1;
        }

        if (toDownload.Count > 0)
        {
            // Have missing files, need to download them.

            Log.Debug(
                "Missing {MissingContentBlobs} blobs, downloading from {ManifestDownloadUrl}",
                toDownload.Count,
                buildInfo.ManifestDownloadUrl!);

            await CheckManifestDownloadServerProtocolVersions(buildInfo.ManifestDownloadUrl!, cancel);

            // Alright well we support the protocol. Now to start the HTTP request!

            var requestBody = new byte[toDownload.Count * 4];
            var reqI = 0;
            foreach (var (_, idx) in toDownload)
            {
                BinaryPrimitives.WriteInt32LittleEndian(requestBody.AsSpan(reqI, 4), idx);
                reqI += 4;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, buildInfo.ManifestDownloadUrl);
            request.Headers.Add(
                "X-Robust-Download-Protocol",
                ManifestDownloadProtocolVersion.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add("Accept-Encoding", "zstd");

            request.Content = new ByteArrayContent(requestBody);

            Log.Debug("Starting download...");

            Status = UpdateStatus.DownloadingClientUpdate;

            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(cancel);
            if (response.Content.Headers.TryGetValues("Content-Encoding", out var ce) && ce.First() == "zstd")
            {
                Log.Debug("Download is using ZStd");
                stream = new ZStdDecompressStream(stream);
            }

            using (stream)
            using (var compressContext = new ZStdCCtx())
            {
                // compressContext.SetParameter(ZSTD_cParameter.ZSTD_c_nbWorkers, 4);
                var swZstd = new Stopwatch();
                var swSqlite = new Stopwatch();

                SqliteBlobStream? blob = null;
                try
                {
                    // Re-use compression buffer and compressor for all files, creating/freeing them is expensive.
                    var compressBuffer = new byte[1024];
                    var readBuffer = new byte[1024];

                    var i = 0;
                    foreach (var (rowId, _) in toDownload)
                    {
                        Progress = (i++, toDownload.Count, ProgressUnit.None);

                        cancel.ThrowIfCancellationRequested();

                        // Read length.
                        var header = await stream.ReadExactAsync(4, cancel);

                        var length = BinaryPrimitives.ReadInt32LittleEndian(header);

                        EnsureBuffer(ref readBuffer, length);
                        var data = readBuffer.AsMemory(0, length);
                        await stream.ReadExactAsync(data, cancel);

                        var uncompressedLen = data.Length;
                        var hash = SHA256.HashData(data.Span);

                        // Double check hash!
                        var expectedHash = con.ExecuteScalar<byte[]>(
                            "SELECT Hash FROM Content WHERE Id = @Id",
                            new { Id = rowId });

                        if (!expectedHash.AsSpan().SequenceEqual(hash))
                            throw new UpdateException("Hash mismatch while downloading!");

                        swZstd.Start();
                        // Try compression.
                        EnsureBuffer(ref compressBuffer, ZStd.CompressBound(data.Length));
                        var compressLength = compressContext.Compress(compressBuffer, data.Span);

                        swZstd.Stop();

                        var compression = 0;
                        var writeData = data;

                        if (compressLength + 10 < uncompressedLen)
                        {
                            compression = 2;
                            writeData = compressBuffer.AsMemory(0, compressLength);
                        }

                        swSqlite.Start();
                        con.Execute(
                            "UPDATE Content SET Size = @Size, Data = zeroblob(@DataSize), Compression = @Compression WHERE Id = @Id",
                            new
                            {
                                Id = rowId, Size = uncompressedLen, DataSize = writeData.Length,
                                Compression = compression
                            });

                        if (blob == null)
                            blob = SqliteBlobStream.Open(con.Handle!, "main", "Content", "Data", rowId, true);
                        else
                            blob.Reopen(rowId);

                        blob.Write(writeData.Span);
                        swSqlite.Stop();

                        // Log.Debug("Data size: {DataSize}, Size: {UncompressedLen}", writeData.Length, uncompressedLen);
                    }
                }
                finally
                {
                    blob?.Dispose();
                }

                Log.Debug("ZSTD: {ZStdElapsed} ms | SQLite: {SqliteElapsed} ms",
                    swZstd.ElapsedMilliseconds,
                    swSqlite.ElapsedMilliseconds);
            }
        }

        return manifestHash;
    }

    private static void EnsureBuffer(ref byte[] buf, int needsFit)
    {
        if (buf.Length >= needsFit)
            return;

        var newLen = 2 << BitOperations.Log2((uint)needsFit-1);

        Array.Resize(ref buf, newLen);
    }

    private async Task CheckManifestDownloadServerProtocolVersions(string url, CancellationToken cancel)
    {
        // Check that we support the required protocol versions for the download server.

        Log.Debug("Checking supported protocols on download server...");

        // Do HTTP OPTIONS to figure out supported download protocol versions.
        var request = new HttpRequestMessage(HttpMethod.Options, url);

        var resp = await _http.SendAsync(request, cancel);
        resp.EnsureSuccessStatusCode();

        if (!resp.Headers.TryGetValues("X-Robust-Download-Min-Protocol", out var minHeaders)
            || !resp.Headers.TryGetValues("X-Robust-Download-Max-Protocol", out var maxHeaders))
        {
            throw new UpdateException("Missing required headers from OPTIONS on manifest download URL!");
        }

        if (!int.TryParse(minHeaders.First(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var min)
            || !int.TryParse(maxHeaders.First(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var max))
        {
            throw new UpdateException("Invalid version headers on OPTIONS on manifest download URL!");
        }

        Log.Debug("Download server protocol min: {MinProtocolVersion} max: {MaxProtocolVersion}", min, max);

        if (min > ManifestDownloadProtocolVersion || max < ManifestDownloadProtocolVersion)
        {
            throw new UpdateException("No supported protocol version for download server.");
        }
    }

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverloadWithCancellation")]
    [SuppressMessage("ReSharper", "UseAwaitUsing")]
    private async Task<byte[]> DownloadNewVersionZip(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        long versionId,
        CancellationToken cancel)
    {
        // Temp file to download zip into.
        await using var tempFile = TempFile.CreateTempFile();

        var zipHash = await UpdateDownloadContent(tempFile, buildInfo, cancel);

        con.Execute("UPDATE ContentVersion SET ZipHash=@ZipHash WHERE Id=@Version",
            new { ZipHash = zipHash, Version = versionId });

        Status = UpdateStatus.LoadingIntoDb;

        tempFile.Seek(0, SeekOrigin.Begin);

        // File downloaded, time to dump this into the DB.

        var zip = new ZipArchive(tempFile, ZipArchiveMode.Read, leaveOpen: true);

        // TODO: hash incrementally without buffering in-memory
        var manifestStream = new MemoryStream();
        var manifestWriter = new StreamWriter(manifestStream, new UTF8Encoding(false));
        manifestWriter.Write("Robust Content Manifest 1\n");

        var hasher = SHA256.Create();
        var totalSize = 0L;
        var sw = new Stopwatch();

        var newFileCount = 0;

        SqliteBlobStream? blob = null;
        try
        {
            // Re-use compression buffer and compressor for all files, creating/freeing them is expensive.
            var compressBuffer = new MemoryStream();
            using var zStdCompressor = new ZStdCompressStream(compressBuffer);

            // Sort by full name for manifest building.
            var count = 0;
            foreach (var entry in zip.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
            {
                cancel.ThrowIfCancellationRequested();

                if (count++ % 100 == 0)
                    Progress = (count++, zip.Entries.Count, ProgressUnit.None);

                // Ignore directory entries.
                if (entry.Name == "")
                    continue;

                Log.Verbose("Storing file {EntryName}", entry.FullName);

                byte[] hash;
                using (var stream = entry.Open())
                {
                    hash = hasher.ComputeHash(stream);
                }

                var row = con.QueryFirstOrDefault<long>(
                    "SELECT Id FROM Content WHERE Hash = @Hash",
                    new { Hash = hash });
                if (row == 0)
                {
                    newFileCount += 1;

                    // Don't have this content blob yet, insert it into the database.
                    using var entryStream = entry.Open();

                    var compress = entry.Length - entry.CompressedLength > 10;
                    if (compress)
                    {
                        sw.Start();
                        entryStream.CopyTo(zStdCompressor, (int)Zstd.ZSTD_CStreamInSize());
                        // Flush to end fragment (i.e. file)
                        zStdCompressor.FlushEnd();
                        sw.Stop();

                        totalSize += compressBuffer.Length;

                        row = con.ExecuteScalar<long>(
                            @"INSERT INTO Content(Hash, Size, Compression, Data)
                        VALUES (@Hash, @Size, @Compression, zeroblob(@BlobLen))
                        RETURNING Id",
                            new
                            {
                                Hash = hash,
                                Size = entry.Length,
                                BlobLen = compressBuffer.Length,
                                Compression = ContentCompressionScheme.ZStd
                            });

                        if (blob == null)
                            blob = SqliteBlobStream.Open(con.Handle!, "main", "Content", "Data", row, true);
                        else
                            blob.Reopen(row);

                        // Write memory buffer to SQLite and reset it.
                        blob.Write(compressBuffer.GetBuffer().AsSpan(0, (int)compressBuffer.Length));
                        compressBuffer.Position = 0;
                        compressBuffer.SetLength(0);
                    }
                    else
                    {
                        row = con.ExecuteScalar<long>(
                            @"INSERT INTO Content(Hash, Size, Compression, Data)
                            VALUES (@Hash, @Size, @Compression, zeroblob(@Size))
                            RETURNING Id",
                            new { Hash = hash, Size = entry.Length, Compression = ContentCompressionScheme.None });

                        if (blob == null)
                            blob = SqliteBlobStream.Open(con.Handle!, "main", "Content", "Data", row, true);
                        else
                            blob.Reopen(row);

                        entryStream.CopyTo(blob);
                    }
                }

                con.Execute(
                    "INSERT INTO ContentManifest(VersionId, Path, ContentId) VALUES (@VersionId, @Path, @ContentId)",
                    new
                    {
                        VersionId = versionId,
                        Path = entry.FullName,
                        ContentId = row,
                    });

                manifestWriter.Write($"{Convert.ToHexString(hash)} {entry.FullName}\n");
            }
        }
        finally
        {
            blob?.Dispose();
        }

        Log.Debug("Compression report: {ElapsedMs} ms elapsed, {TotalSize} B total size", sw.ElapsedMilliseconds,
            totalSize);
        Log.Debug("New files: {NewFilesCount}", newFileCount);

        manifestWriter.Flush();

        manifestStream.Seek(0, SeekOrigin.Begin);

        var manifestHash = HashFile(manifestStream);

        return manifestHash;
    }

    /// <summary>
    /// Download content zip to the specified file and verify hash.
    /// </summary>
    /// <returns>
    /// File hash in case the server didn't provide one.
    /// </returns>
    private async Task<byte[]> UpdateDownloadContent(
        Stream file,
        ServerBuildInformation buildInformation,
        CancellationToken cancel)
    {
        Status = UpdateStatus.DownloadingClientUpdate;

        Log.Information("Downloading content update from {ContentDownloadUrl}", buildInformation.DownloadUrl);

        await _http.DownloadToStream(
            buildInformation.DownloadUrl,
            file,
            DownloadProgressCallback,
            cancel);

        file.Position = 0;

        Progress = null;

        Status = UpdateStatus.Verifying;

        var hash = await Task.Run(() => HashFile(file), cancel);
        file.Position = 0;

        var newFileHashString = Convert.ToHexString(hash);
        if (buildInformation.Hash is { } expectHash)
        {
            if (!expectHash.Equals(newFileHashString, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Hash mismatch. Expected: {expectHash}, got: {newFileHashString}");
            }
        }

        Log.Verbose("Done downloading zip. Hash: {DownloadHash}", newFileHashString);

        return hash;
    }

    private async Task CullEngineVersionsMaybe(SqliteConnection contentConnection)
    {
        await _engineManager.DoEngineCullMaybeAsync(contentConnection);
    }

    private async Task<bool> InstallEngineVersionIfMissing(string engineVer, CancellationToken cancel)
    {
        Status = UpdateStatus.DownloadingEngineVersion;
        var change = await _engineManager.DownloadEngineIfNecessary(engineVer, DownloadProgressCallback, cancel);

        Progress = null;
        return change;
    }

    private void DownloadProgressCallback(long downloaded, long total)
    {
        Dispatcher.UIThread.Post(() => Progress = (downloaded, total, ProgressUnit.Bytes));
    }

    internal static byte[] HashFile(Stream stream)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(stream);
    }

    public static string[] GetModuleNames(Stream manifestContent)
    {
        // Check zip file contents for manifest.yml and read the modules the server needs.
        using var streamReader = new StreamReader(manifestContent);
        var manifestData = ResourceManifestDeserializer.Deserialize<ResourceManifestData?>(streamReader);
        if (manifestData != null)
            return manifestData.Modules;

        return Array.Empty<string>();
    }

    private sealed class ResourceManifestData
    {
        public string[] Modules = Array.Empty<string>();
    }

    public enum UpdateStatus
    {
        CheckingClientUpdate,
        CheckingEngineModules,
        DownloadingEngineVersion,
        DownloadingEngineModules,
        DownloadingClientUpdate,
        Verifying,
        CommittingDownload,
        LoadingIntoDb,
        CullingEngine,
        CullingContent,
        Ready,
        Error,
    }

    public enum ProgressUnit
    {
        None,
        Bytes,
    }
}
