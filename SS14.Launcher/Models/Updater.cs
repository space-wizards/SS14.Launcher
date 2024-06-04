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
using System.Net.Http.Headers;
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
using SpaceWizards.Sodium;
using Splat;
using SQLitePCL;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Utility;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static SQLitePCL.raw;

namespace SS14.Launcher.Models;

public sealed class Updater : ReactiveObject
{
    private const int ManifestDownloadProtocolVersion = 1;

    // How many bytes a compression attempt needs to save to be considered "worth it".
    private const int CompressionSavingsThreshold = 10;

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
    [Reactive] public long? Speed { get; private set; }

    public async Task<ContentLaunchInfo?> RunUpdateForLaunchAsync(
        ServerBuildInformation buildInformation,
        CancellationToken cancel = default)
    {
        return await GuardUpdateAsync(() => RunUpdate(buildInformation, cancel));
    }

    public async Task<ContentLaunchInfo?> InstallContentBundleForLaunchAsync(
        ZipArchive archive,
        byte[] zipHash,
        ContentBundleMetadata metadata,
        CancellationToken cancel = default)
    {
        return await GuardUpdateAsync(() => InstallContentBundle(archive, zipHash, metadata, cancel));
    }

    private async Task<T?> GuardUpdateAsync<T>(Func<Task<T>> func) where T : class
    {
        if (_updating)
        {
            throw new InvalidOperationException("Update already in progress.");
        }

        _updating = true;

        try
        {
            var ret = await func();
            Status = UpdateStatus.Ready;
            return ret;
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
            Progress = null;
            Speed = null;
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
            () => TouchOrDownloadContentUpdateTransacted(buildInfo, con, moduleManifest, cancel),
            CancellationToken.None);

        Log.Debug("Checking to cull old content versions...");

        await Task.Run(() => { CullOldContentVersions(con); }, CancellationToken.None);

        return await InstallEnginesForVersion(con, moduleManifest, versionRowId, cancel);
    }

    private async Task<ContentLaunchInfo> InstallContentBundle(
        ZipArchive archive,
        byte[] zipHash,
        ContentBundleMetadata metadata,
        CancellationToken cancel)
    {
        // ReSharper disable once UseAwaitUsing
        using var con = ContentManager.GetSqliteConnection();

        Status = UpdateStatus.LoadingContentBundle;

        // Both content downloading and engine downloading MAY need the manifest.
        // So use a Lazy<Task<T>> to avoid loading it twice.
        var moduleManifest = new Lazy<Task<EngineModuleManifest>>(
            () => _engineManager.GetEngineModuleManifest(cancel)
        );

        var versionId = await Task.Run(async () =>
        {
            // ReSharper disable once UseAwaitUsing
            using var transaction = con.BeginTransaction();

            // The launcher interprets a "content bundle" zip differently from one loaded via server download.
            // As such, we must keep these distinct in the database, even if the file is the same.
            // We do this by just doing another unique transformation on the hash.
            var transformedZipHash = TransformContentBundleZipHash(zipHash);
            var transformedZipHashHex = Convert.ToHexString(transformedZipHash);

            Log.Debug(
                "Real zip file hash is {Hash}. Transformed is {TransformedHash}",
                Convert.ToHexString(zipHash),
                transformedZipHashHex
            );

            Log.Debug("Checking if we already have this content bundle ingested...");
            var existing = CheckExisting(
                con,
                new ServerBuildInformation { Hash = transformedZipHashHex }
            );

            long versionId;
            if (existing == null)
            {
                versionId = con.ExecuteScalar<long>(
                    @"INSERT INTO ContentVersion(Hash, ForkId, ForkVersion, LastUsed, ZipHash)
                    VALUES (zeroblob(32), 'AnonymousContentBundle', @ForkVersion, datetime('now'), @ZipHash)
                    RETURNING Id",
                    new
                    {
                        ZipHash = transformedZipHash,
                        ForkVersion = transformedZipHashHex
                    }
                );

                Log.Debug("Did not already have this content bundle, ingesting as new version {Version}", versionId);

                if (metadata.BaseBuild is { } baseBuildData)
                {
                    Log.Debug("Content bundle has base build info, downloading...");

                    // We have a base build to download.
                    // Copy it into the new AnonymousContentBundle version before loading the rest of the zip contents.
                    var baseBuildId = await TouchOrDownloadContentUpdate(
                        new ServerBuildInformation
                        {
                            DownloadUrl = baseBuildData.DownloadUrl,
                            ManifestUrl = baseBuildData.ManifestUrl,
                            ManifestDownloadUrl = baseBuildData.ManifestDownloadUrl,
                            EngineVersion = metadata.EngineVersion,
                            Version = baseBuildData.Version,
                            ForkId = baseBuildData.ForkId,
                            Hash = baseBuildData.Hash,
                            ManifestHash = baseBuildData.ManifestHash,
                            Acz = false
                        },
                        con,
                        moduleManifest,
                        cancel
                    );

                    // Copy base build manifest into new version
                    con.Execute(
                        @"INSERT INTO ContentManifest (VersionId, Path, ContentId)
                        SELECT @NewVersion, Path, ContentId
                        FROM ContentManifest
                        WHERE VersionId = @OldVersion",
                        new
                        {
                            NewVersion = versionId,
                            OldVersion = baseBuildId
                        }
                    );
                }

                Status = UpdateStatus.LoadingIntoDb;

                Log.Debug("Ingesting zip file...");
                IngestZip(con, versionId, archive, true, cancel);

                // Insert real manifest hash into the database.
                var manifestHash = GenerateContentManifestHash(con, versionId);
                con.Execute("UPDATE ContentVersion SET Hash = @Hash WHERE Id = @Version",
                    new { Hash = manifestHash, Version = versionId });

                Log.Debug("Manifest hash of new version is {Hash}", Convert.ToHexString(manifestHash));
                Log.Debug("Resolving content dependencies...");

                // TODO: This could copy from base build modules in certain cases.
                await ResolveContentDependencies(con, versionId, metadata.EngineVersion, moduleManifest);
            }
            else
            {
                Log.Debug("Already had content bundle, updating last used time.");

                TouchVersion(con, existing.Id);
                versionId = existing.Id;
            }

            Status = UpdateStatus.CommittingDownload;

            transaction.Commit();

            Log.Debug("Checking to cull old content versions...");

            CullOldContentVersions(con);

            return versionId;
        }, CancellationToken.None);

        return await InstallEnginesForVersion(con, moduleManifest, versionId, cancel);
    }

    private async Task<ContentLaunchInfo> InstallEnginesForVersion(
        SqliteConnection con,
        Lazy<Task<EngineModuleManifest>> moduleManifest,
        long versionRowId,
        CancellationToken cancel)
    {
        (string, string)[] modules;

        {
            Status = UpdateStatus.CheckingClientUpdate;
            modules = con.Query<(string, string)>(
                "SELECT ModuleName, moduleVersion FROM ContentEngineDependency WHERE VersionId = @Version",
                new { Version = versionRowId }).ToArray();

            for (var index = 0; index < modules.Length; index++)
            {
                var (name, version) = modules[index];
                if (name == "Robust")
                {
                    // Engine version may change here due to manifest version redirects.
                    var newEngineVersion = await InstallEngineVersionIfMissing(version, cancel);
                    modules[index] = (name, newEngineVersion);
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

        // We ORDER BY ... DESC so that a hopeful exact match always comes first.
        // This way, we avoid DuplicateExistingVersion() unless absolutely necessary.

        ContentVersion? found;
        if (buildInfo.ManifestHash is { } manifestHashHex)
        {
            // Manifest hash is ultimate source of truth.
            var hash = Convert.FromHexString(manifestHashHex);

            found = con.QueryFirstOrDefault<ContentVersion>(
                "SELECT * FROM ContentVersion cv " +
                "WHERE Hash = @Hash " +
                "ORDER BY ForkVersion = @ForkVersion " +
                "AND ForkId = @ForkId " +
                "AND (SELECT ModuleVersion FROM ContentEngineDependency ced WHERE ced.VersionId = cv.Id AND ModuleName = 'Robust') = @EngineVersion " +
                "DESC",
                new { Hash = hash, ForkVersion = buildInfo.Version, buildInfo.ForkId, buildInfo.EngineVersion });
        }
        else if (buildInfo.Hash is { } hashHex)
        {
            // If the server ONLY provides a zip hash, look up purely by it.
            var hash = Convert.FromHexString(hashHex);

            found = con.QueryFirstOrDefault<ContentVersion>(
                "SELECT * FROM ContentVersion cv WHERE ZipHash = @ZipHash " +
                "ORDER BY ForkVersion = @ForkVersion " +
                "AND ForkId = @ForkId " +
                "AND (SELECT ModuleVersion FROM ContentEngineDependency ced WHERE ced.VersionId = cv.Id AND ModuleName = 'Robust') = @EngineVersion " +
                "DESC",
                new { ZipHash = hash, ForkVersion = buildInfo.Version, buildInfo.ForkId, buildInfo.EngineVersion });
        }
        else
        {
            // If no hash, just use forkID/Version and hope for the best.
            // Why do I even support this?
            // Testing I guess?

            found = con.QueryFirstOrDefault<ContentVersion>(
                "SELECT * FROM ContentVersion cv WHERE ForkId = @ForkId AND ForkVersion = @Version " +
                "ORDER BY (SELECT ModuleVersion FROM ContentEngineDependency ced WHERE ced.VersionId = cv.Id AND ModuleName = 'Robust') = @EngineVersion " +
                "DESC",
                new { buildInfo.ForkId, buildInfo.Version, buildInfo.EngineVersion });
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

        return versionId;
    }

    private async Task<long> TouchOrDownloadContentUpdateTransacted(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        Lazy<Task<EngineModuleManifest>> moduleManifest,
        CancellationToken cancel)
    {
        // ReSharper disable once UseAwaitUsing
        using var transaction = con.BeginTransaction();

        var versionId = await TouchOrDownloadContentUpdate(buildInfo, con, moduleManifest, cancel);

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
            Log.Debug("Mismatching ContentVersion info, duplicating to new entry");

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
                    SELECT @NewVersion, Path, ContentId
                    FROM ContentManifest
                    WHERE VersionId = @OldVersion",
                new
                {
                    NewVersion = versionId,
                    OldVersion = existingVersion.Id
                });

            if (changedEngineVersion)
            {
                con.Execute(@"
                        INSERT INTO ContentEngineDependency (VersionId, ModuleName, ModuleVersion)
                        VALUES (@VersionId, 'Robust', @EngineVersion)",
                    new
                    {
                        EngineVersion = engineVersion,
                        VersionId = versionId
                    });

                // Recalculate module dependencies.
                var oldDependencies = con.Query<string>(@"
                        SELECT ModuleName
                        FROM ContentEngineDependency
                        WHERE VersionId = @OldVersion AND ModuleName != 'Robust'", new
                {
                    OldVersion = existingVersion.Id
                }).ToArray();

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
                    SELECT @NewVersion, ModuleName, ModuleVersion
                    FROM ContentEngineDependency
                    WHERE VersionId = @OldVersion",
                    new
                    {
                        NewVersion = versionId,
                        OldVersion = existingVersion.Id
                    });
            }
        }
        else
        {
            versionId = existingVersion.Id;
            // If we do have an exact match we are not changing anything, *except* the LastUsed column.
            TouchVersion(con, versionId);
        }

        return versionId;
    }

    private static void TouchVersion(SqliteConnection con, long versionId)
    {
        con.Execute("UPDATE ContentVersion SET LastUsed = datetime('now') WHERE Id = @Version",
            new { Version = versionId });
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
        else if (buildInfo.DownloadUrl != null)
        {
            manifestHash = await DownloadNewVersionZip(buildInfo, con, versionId, cancel);
        }
        else
        {
            throw new InvalidOperationException("No download information provided at all!");
        }

        Log.Debug("Manifest hash: {ManifestHash}", Convert.ToHexString(manifestHash));

        con.Execute(
            "UPDATE ContentVersion SET Hash = @Hash WHERE Id = @Id",
            new { Hash = manifestHash, Id = versionId });

        // Insert engine dependencies.

        await ResolveContentDependencies(con, versionId, engineVersion, moduleManifest);

        return versionId;
    }

    private static async Task ResolveContentDependencies(
        SqliteConnection con,
        long versionId,
        string engineVersion,
        Lazy<Task<EngineModuleManifest>> moduleManifest)
    {
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
        if (LoadManifestData(con, versionId) is not { } manifestData)
            return;

        var modules = manifestData.Modules;

        if (modules.Length <= 0)
            return;

        var manifest = await moduleManifest.Value;

        foreach (var module in modules)
        {
            var version = IEngineManager.ResolveEngineModuleVersion(manifest, module, engineVersion);

            con.Execute(
                @"INSERT INTO ContentEngineDependency(VersionId, ModuleName, ModuleVersion)
                        VALUES (@Version, @ModName, @ModVersion)",
                new
                {
                    Version = versionId,
                    ModName = module,
                    ModVersion = version
                });

            Log.Debug("Inserting dependency: {ModuleName} {ModuleVersion}", module, version);
        }
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
        var swZstd = new Stopwatch();
        var swSqlite = new Stopwatch();
        var swBlake = new Stopwatch();

        // Download manifest first.

        Status = UpdateStatus.FetchingClientManifest;

        var (toDownload, manifestHash) = await IngestContentManifest(buildInfo, con, versionId, swSqlite, cancel);

        Progress = null;
        Status = UpdateStatus.DownloadingClientUpdate;

        if (toDownload.Count > 0)
        {
            // Have missing files, need to download them.

            Log.Debug(
                "Missing {MissingContentBlobs} blobs, downloading from {ManifestDownloadUrl}",
                toDownload.Count,
                buildInfo.ManifestDownloadUrl!);

            await DownloadMissingContent(
                buildInfo,
                con,
                toDownload,
                swSqlite,
                swZstd,
                swBlake,
                cancel);
        }

        Log.Debug("ZSTD: {ZStdElapsed} ms | SQLite: {SqliteElapsed} ms | Blake2B: {Blake2BElapsed} ms",
            swZstd.ElapsedMilliseconds,
            swSqlite.ElapsedMilliseconds,
            swBlake.ElapsedMilliseconds);

#if DEBUG
        var testHash = GenerateContentManifestHash(con, versionId);
        Debug.Assert(testHash.AsSpan().SequenceEqual(manifestHash));
#endif

        return manifestHash;
    }

    private async Task<(List<(long rowid, int index)> toDownload, byte[] manifestHash)> IngestContentManifest(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        long versionId,
        Stopwatch swSqlite,
        CancellationToken cancel)
    {
        Log.Debug("Downloading content manifest from {ContentManifestUrl}", buildInfo.ManifestUrl);

        var request = new HttpRequestMessage(HttpMethod.Get, buildInfo.ManifestUrl);
        var manifestResp = await _http.SendZStdAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
        manifestResp.EnsureSuccessStatusCode();

        var manifest = Blake2BHasherStream.CreateReader(
            await manifestResp.Content.ReadAsStreamAsync(cancel),
            ReadOnlySpan<byte>.Empty,
            32);

        // Go over the manifest, reading it into the SQLite ContentManifest table.
        // For any content blobs we don't have yet, we put a placeholder entry in the database for now.
        // Keep track of all files we need to download for later.

        using var sr = new StreamReader(manifest);

        if (await sr.ReadLineAsync() != "Robust Content Manifest 1")
            throw new UpdateException("Unknown manifest header!");

        var toDownload = new List<(long rowid, int index)>();

        Log.Debug("Parsing manifest into database...");

        // Prepare SQLite queries.

        var err = sqlite3_prepare_v2(
            con.Handle,
            "SELECT Id FROM Content WHERE Hash = ?",
            out var stmtFindContentRow);
        CheckErr(err);

        using var _ = stmtFindContentRow;

        err = sqlite3_prepare_v2(
            con.Handle,
            "INSERT INTO Content (Hash, Size, Compression, Data) VALUES (@Hash, 0, 0, zeroblob(0)) RETURNING Id",
            out var stmtContentPlaceholder);
        CheckErr(err);

        using var a = stmtContentPlaceholder;

        err = sqlite3_prepare_v2(
            con.Handle,
            "INSERT INTO ContentManifest(VersionId, Path, ContentId) VALUES (@VersionId, @Path, @ContentId)",
            out var stmtInsertManifest);
        CheckErr(err);

        // VersionId does not change, bind it early.
        CheckErr(sqlite3_bind_int64(stmtInsertManifest, 1, versionId));

        using var b = stmtInsertManifest;

        var lineIndex = 0;
        while (await sr.ReadLineAsync() is { } manifestLine)
        {
            cancel.ThrowIfCancellationRequested();

            var sep = manifestLine.IndexOf(' ');
            var hash = Convert.FromHexString(manifestLine.AsSpan(0, sep));
            var filename = manifestLine.AsMemory(sep + 1);

            // Look up if we have an existing blob by that hash.

            swSqlite.Start();
            err = sqlite3_bind_blob(stmtFindContentRow, 1, hash);
            SqliteException.ThrowExceptionForRC(err, con.Handle);

            err = sqlite3_step(stmtFindContentRow);
            SqliteException.ThrowExceptionForRC(err, con.Handle);

            long row;
            if (err == SQLITE_DONE)
            {
                CheckErr(sqlite3_reset(stmtFindContentRow));

                // Insert placeholder
                // INSERT INTO Content (Hash, Size, Compression, Data) VALUES (@Hash, 0, 0, zeroblob(0)) RETURNING Id

                CheckErr(sqlite3_bind_blob(stmtContentPlaceholder, 1, hash)); // @Hash
                CheckErr(sqlite3_step(stmtContentPlaceholder));

                row = sqlite3_column_int64(stmtContentPlaceholder, 0);
                CheckErr(sqlite3_reset(stmtContentPlaceholder));

                toDownload.Add((row, lineIndex));
            }
            else
            {
                Debug.Assert(err == SQLITE_ROW);

                row = sqlite3_column_int64(stmtFindContentRow, 0);

                err = sqlite3_reset(stmtFindContentRow);
                SqliteException.ThrowExceptionForRC(err, con.Handle);
            }

            // INSERT INTO ContentManifest(VersionId, Path, ContentId) VALUES (@VersionId, @Path, @ContentId)
            // @VersionId is bound at statement creation.
            CheckErr(sqlite3_bind_text16(stmtInsertManifest, 2, filename.Span)); // @Path
            CheckErr(sqlite3_bind_int64(stmtInsertManifest, 3, row)); // @ContentId

            CheckErr(sqlite3_step(stmtInsertManifest));
            CheckErr(sqlite3_reset(stmtInsertManifest));
            swSqlite.Stop();

            lineIndex += 1;
        }

        Log.Debug("Total of {ManifestEntriesCount} manifest entries", lineIndex);

        var manifestHash = manifest.Finish();
        if (Convert.ToHexString(manifestHash) != buildInfo.ManifestHash)
            throw new UpdateException("Manifest has incorrect hash!");

        Log.Debug("Successfully validated manifest hash");

        return (toDownload, manifestHash);

        void CheckErr(int err) => SqliteException.ThrowExceptionForRC(err, con.Handle);
    }

    private async Task DownloadMissingContent(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        List<(long rowid, int index)> toDownload,
        Stopwatch swSqlite,
        Stopwatch swZstd,
        Stopwatch swBlake,
        CancellationToken cancel)
    {
        await CheckManifestDownloadServerProtocolVersions(buildInfo.ManifestDownloadUrl!, cancel);

        // Alright well we support the protocol. Now to start the HTTP request!


        // Write request body.
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

        request.Content = new ByteArrayContent(requestBody);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        Log.Debug("Starting download...");

        Status = UpdateStatus.DownloadingClientUpdate;

        // Send HTTP request

        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("zstd"));
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancel);
        var bandwidthStream = new BandwidthStream(stream);
        stream = bandwidthStream;
        if (response.Content.Headers.ContentEncoding.Contains("zstd"))
            stream = new ZStdDecompressStream(stream);

        await using var streamDispose = stream;

        // Read flags header
        var streamHeader = await stream.ReadExactAsync(4, cancel);
        var streamFlags = (DownloadStreamHeaderFlags)BinaryPrimitives.ReadInt32LittleEndian(streamHeader);
        var preCompressed = (streamFlags & DownloadStreamHeaderFlags.PreCompressed) != 0;

        // compressContext.SetParameter(ZSTD_cParameter.ZSTD_c_nbWorkers, 4);
        // If the stream is pre-compressed we need to decompress the blobs to verify BLAKE2B hash.
        // If it isn't, we need to manually try re-compressing individual files to store them.
        var compressContext = preCompressed ? null : new ZStdCCtx();
        var decompressContext = preCompressed ? new ZStdDCtx() : null;

        // Normal file header:
        // <int32> uncompressed length
        // When preCompressed is set, we add:
        // <int32> compressed length
        var fileHeader = new byte[preCompressed ? 8 : 4];

        SqliteBlobStream? blob = null;
        sqlite3_stmt? stmtFindContentHash = null;
        sqlite3_stmt? stmtUpdateContent = null;
        try
        {
            var err = sqlite3_prepare_v2(
                con.Handle,
                "SELECT Hash FROM Content WHERE Id = ?",
                out stmtFindContentHash);
            SqliteException.ThrowExceptionForRC(err, con.Handle);

            err = sqlite3_prepare_v2(
                con.Handle,
                "UPDATE Content SET Size = @Size, Data = zeroblob(@DataSize), Compression = @Compression WHERE Id = @Id",
                out stmtUpdateContent);
            SqliteException.ThrowExceptionForRC(err, con.Handle);

            // Buffer for storing compressed ZStd data.
            var compressBuffer = new byte[1024];

            // Buffer for storing uncompressed data.
            var readBuffer = new byte[1024];

            var hash = new byte[256 / 8];

            var i = 0;
            foreach (var (rowId, _) in toDownload)
            {
                // Simple loop stuff.
                cancel.ThrowIfCancellationRequested();

                Progress = (i, toDownload.Count, ProgressUnit.None);
                Speed = bandwidthStream.CalcCurrentAvg();

                // Read file header.
                await stream.ReadExactAsync(fileHeader, cancel);

                var length = BinaryPrimitives.ReadInt32LittleEndian(fileHeader.AsSpan(0, 4));

                EnsureBuffer(ref readBuffer, length);
                var data = readBuffer.AsMemory(0, length);

                // Data to write to database.
                var compression = ContentCompressionScheme.None;
                var writeData = data;

                if (preCompressed)
                {
                    // Compressed length from extended header.
                    var compressedLength = BinaryPrimitives.ReadInt32LittleEndian(fileHeader.AsSpan(4, 4));

                    // Log.Debug("{index:D5}: {blobLength:D8} {dataLength:D8}", idx, length, compressedLength);

                    if (compressedLength > 0)
                    {
                        EnsureBuffer(ref compressBuffer, compressedLength);
                        var compressedData = compressBuffer.AsMemory(0, compressedLength);
                        await stream.ReadExactAsync(compressedData, cancel);

                        // Decompress so that we can verify hash down below.
                        // TODO: It's possible to hash while we're decompressing to avoid using a full buffer.

                        swZstd.Start();
                        var decompressedLength = decompressContext!.Decompress(data.Span, compressedData.Span);
                        swZstd.Stop();

                        if (decompressedLength != data.Length)
                            throw new UpdateException($"Compressed blob {i} had incorrect decompressed size!");

                        // Set variables so that the database write down below uses them.
                        compression = ContentCompressionScheme.ZStd;
                        writeData = compressedData;
                    }
                    else
                    {
                        await stream.ReadExactAsync(data, cancel);
                    }
                }
                else
                {
                    await stream.ReadExactAsync(data, cancel);
                }

                swBlake.Start();
                CryptoGenericHashBlake2B.Hash(hash, data.Span, ReadOnlySpan<byte>.Empty);
                swBlake.Stop();

                // Double check hash!
                swSqlite.Start();

                CheckErr(sqlite3_bind_int64(stmtFindContentHash, 1, rowId));

                CheckErr(sqlite3_step(stmtFindContentHash));

                if (!sqlite3_column_blob(stmtFindContentHash, 0).SequenceEqual(hash))
                    throw new UpdateException("Hash mismatch while downloading!");

                CheckErr(sqlite3_reset(stmtFindContentHash));

                swSqlite.Stop();

                if (!preCompressed)
                {
                    // File wasn't pre-compressed. We should try to manually compress it to save space in DB.

                    swZstd.Start();

                    EnsureBuffer(ref compressBuffer, ZStd.CompressBound(data.Length));
                    var compressLength = compressContext!.Compress(compressBuffer, data.Span);

                    swZstd.Stop();

                    // Don't bother saving compressed data if it didn't save enough space.
                    if (compressLength + CompressionSavingsThreshold < length)
                    {
                        // Set variables so that the database write down below uses them.
                        compression = ContentCompressionScheme.ZStd;
                        writeData = compressBuffer.AsMemory(0, compressLength);
                    }
                }

                swSqlite.Start();

                // UPDATE Content SET Size = @Size, Data = zeroblob(@DataSize), Compression = @Compression WHERE Id = @Id

                CheckErr(sqlite3_bind_int(stmtUpdateContent, 1, length)); // @Size
                CheckErr(sqlite3_bind_int(stmtUpdateContent, 2, writeData.Length)); // @DataSize
                CheckErr(sqlite3_bind_int(stmtUpdateContent, 3, (int)compression)); // @Compression
                CheckErr(sqlite3_bind_int64(stmtUpdateContent, 4, rowId)); // @Id

                CheckErr(sqlite3_step(stmtUpdateContent));

                CheckErr(sqlite3_reset(stmtUpdateContent));

                if (blob == null)
                    blob = SqliteBlobStream.Open(con.Handle!, "main", "Content", "Data", rowId, true);
                else
                    blob.Reopen(rowId);

                blob.Write(writeData.Span);
                swSqlite.Stop();

                // Log.Debug("Data size: {DataSize}, Size: {UncompressedLen}", writeData.Length, uncompressedLen);
                i += 1;
            }
        }
        finally
        {
            blob?.Dispose();
            decompressContext?.Dispose();
            compressContext?.Dispose();
            stmtFindContentHash?.Dispose();
            stmtUpdateContent?.Dispose();
        }

        Progress = null;
        Speed = null;

        void CheckErr(int err) => SqliteException.ThrowExceptionForRC(err, con.Handle);
    }

    private static void EnsureBuffer(ref byte[] buf, int needsFit)
    {
        if (buf.Length >= needsFit)
            return;

        var newLen = 2 << BitOperations.Log2((uint)needsFit - 1);

        buf = new byte[newLen];
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

        IngestZip(con, versionId, zip, false, cancel);

        return GenerateContentManifestHash(con, versionId);
    }

    private void IngestZip(
        SqliteConnection con,
        long versionId,
        ZipArchive zip,
        bool underlay,
        CancellationToken cancel)
    {
        var totalSize = 0L;
        var sw = new Stopwatch();

        var newFileCount = 0;

        SqliteBlobStream? blob = null;
        try
        {
            // Re-use compression buffer and compressor for all files, creating/freeing them is expensive.
            var compressBuffer = new MemoryStream();
            using var zStdCompressor = new ZStdCompressStream(compressBuffer);

            var count = 0;
            foreach (var entry in zip.Entries)
            {
                cancel.ThrowIfCancellationRequested();

                if (count++ % 100 == 0)
                    Progress = (count++, zip.Entries.Count, ProgressUnit.None);

                // Ignore directory entries.
                if (entry.Name == "")
                    continue;

                if (underlay)
                {
                    // Ignore files from the zip file we already have.
                    var exists = con.ExecuteScalar<bool>(
                        @"SELECT COUNT(*) FROM ContentManifest
                        WHERE Path = @Path AND VersionId = @VersionId",
                        new
                        {
                            Path = entry.FullName,
                            VersionId = versionId
                        }
                    );

                    if (exists)
                        continue;
                }

                // Log.Verbose("Storing file {EntryName}", entry.FullName);

                byte[] hash;
                using (var stream = entry.Open())
                {
                    hash = Blake2B.HashStream(stream, 32);
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
            }
        }
        finally
        {
            blob?.Dispose();
        }

        Log.Debug("Compression report: {ElapsedMs} ms elapsed, {TotalSize} B total size", sw.ElapsedMilliseconds,
            totalSize);
        Log.Debug("New files: {NewFilesCount}", newFileCount);
    }

    private static byte[] GenerateContentManifestHash(SqliteConnection con, long versionId)
    {
        var manifestQuery = con.Query<(string, byte[])>(
            @"SELECT
                Path, Hash
            FROM
                ContentManifest
            INNER JOIN
                Content
            ON
                Content.Id = ContentManifest.ContentId
            WHERE
                ContentManifest.VersionId = @VersionId
            ORDER BY
                Path
            ",
            new
            {
                VersionId = versionId
            }
        );

        var manifestStream = new MemoryStream();
        var manifestWriter = new StreamWriter(manifestStream, new UTF8Encoding(false));
        manifestWriter.Write("Robust Content Manifest 1\n");

        foreach (var (path, hash) in manifestQuery)
        {
            manifestWriter.Write($"{Convert.ToHexString(hash)} {path}\n");
        }

        manifestWriter.Flush();

        manifestStream.Seek(0, SeekOrigin.Begin);

        return Blake2B.HashStream(manifestStream, 32);
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
            buildInformation.DownloadUrl!,
            file,
            DownloadProgressCallback,
            cancel);

        file.Position = 0;

        Progress = null;

        Status = UpdateStatus.Verifying;

        var hash = await Task.Run(() => HashFileSha256(file), cancel);
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

    private async Task<string> InstallEngineVersionIfMissing(string engineVer, CancellationToken cancel)
    {
        Status = UpdateStatus.DownloadingEngineVersion;
        var (changedVersion, _) = await _engineManager.DownloadEngineIfNecessary(engineVer, DownloadProgressCallback, cancel);

        Progress = null;
        return changedVersion;
    }

    private void DownloadProgressCallback(long downloaded, long total)
    {
        Dispatcher.UIThread.Post(() => Progress = (downloaded, total, ProgressUnit.Bytes));
    }

    internal static byte[] HashFileSha256(Stream stream)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(stream);
    }

    public static ResourceManifestData? LoadManifestData(SqliteConnection contentConnection, long versionId)
    {
        if (ContentManager.OpenBlob(contentConnection, versionId, "manifest.yml") is not { } resourceManifest)
            return null;

        using var streamReader = new StreamReader(resourceManifest);
        var manifestData = ResourceManifestDeserializer.Deserialize<ResourceManifestData?>(streamReader);
        return manifestData;
    }

    private static byte[] TransformContentBundleZipHash(ReadOnlySpan<byte> zipHash)
    {
        // Append some data to it and hash it again. No way you're finding a collision against THAT.
        var modifiedData = new byte[zipHash.Length * 2];
        zipHash.CopyTo(modifiedData);

        "content bundle change"u8.CopyTo(modifiedData.AsSpan(zipHash.Length));

        return SHA256.HashData(modifiedData);
    }

    public sealed class ResourceManifestData
    {
        public string[] Modules = Array.Empty<string>();
        public bool MultiWindow = false;
    }

    public enum UpdateStatus
    {
        CheckingClientUpdate,
        CheckingEngineModules,
        DownloadingEngineVersion,
        DownloadingEngineModules,
        FetchingClientManifest,
        DownloadingClientUpdate,
        Verifying,
        CommittingDownload,
        LoadingIntoDb,
        CullingEngine,
        CullingContent,
        Ready,
        Error,
        LoadingContentBundle,
    }

    public enum ProgressUnit
    {
        None,
        Bytes,
    }

    [Flags]
    public enum DownloadStreamHeaderFlags
    {
        None = 0,

        /// <summary>
        /// If this flag is set on the download stream, individual files have been pre-compressed by the server.
        /// This means each file has a compression header, and the launcher should not attempt to compress files itself.
        /// </summary>
        PreCompressed = 1 << 0
    }
}
