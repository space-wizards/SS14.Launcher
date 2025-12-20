using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using Splat;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Utility;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SS14.Launcher.Models;

public sealed partial class Updater : ReactiveObject
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

    public Exception? UpdateException;

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
        UpdateException = null;

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
            UpdateException = e;
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

                if (metadata.BaseBuild is not null)
                {
                    Log.Debug("Content bundle has base build info, downloading...");

                    // We have a base build to download.
                    // Copy it into the new AnonymousContentBundle version before loading the rest of the zip contents.
                    var baseBuildId = await TouchOrDownloadContentUpdate(
                        metadata.GetBaseBuildInformation(),
                        con,
                        moduleManifest,
                        new TransactedDownloadState(),
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
                ZipIngest(con, versionId, archive, true, cancel);

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

        var anythingRemoved = false;

        //
        // Cull old content versions.
        //

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
                anythingRemoved = true;
            }
        }

        //
        // Cull old interrupted downloads.
        //

        var interruptedKeepHours = _cfg.GetCVar(CVars.InterruptibleDownloadKeepHours);
        var affected = con.Execute(
            "DELETE FROM InterruptedDownload WHERE Added < @Threshold",
            new { Threshold = DateTime.UtcNow - TimeSpan.FromHours(interruptedKeepHours) });

        if (affected > 0)
        {
            Log.Debug("Deleted {DeletedCount} old interrupted downloads", affected);
            anythingRemoved = true;
        }

        if (anythingRemoved)
        {
            var rows = con.Execute("""
                DELETE FROM Content
                WHERE Id NOT IN (SELECT ContentId FROM ContentManifest)
                    AND Id NOT IN (SELECT ContentId FROM InterruptedDownloadContent)
                """);
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
        TransactedDownloadState state,
        CancellationToken cancel)
    {
        // Check if we already have this version KNOWN GOOD installed in the content DB.
        var existingVersion = CheckExisting(con, buildInfo);

        long versionId;
        var engineVersion = buildInfo.EngineVersion;
        if (existingVersion == null)
        {
            versionId = await DownloadNewVersion(buildInfo, con, moduleManifest, state, cancel, engineVersion);
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

        var transactedState = new TransactedDownloadState();

        long versionId;
        try
        {
            versionId = await TouchOrDownloadContentUpdate(buildInfo, con, moduleManifest, transactedState, cancel);
        }
        // Avoid catching SQLite exceptions.
        // Those probably indicate it's unsafe for us to go any further so avoid making anything worse.
        catch (Exception e) when (e is not SqliteException)
        {
            if (transactedState.DownloadedContentEntries.Count == 0)
            {
                // Nothing was downloaded. No point saving anything, just go on as normal.
                throw;
            }

            Status = UpdateStatus.CommittingDownload;

            Log.Error(
                "Exception occured while downloading, saving {InterruptedCount} blobs as interrupted",
                transactedState.DownloadedContentEntries.Count);

            SaveInterruptedDownload(con, transactedState);
            ClearIncompleteTransactedState(con, transactedState);

            transaction.Commit();

            throw;
        }

        transaction.Commit();

        return versionId;
    }

    private static void SaveInterruptedDownload(SqliteConnection con, TransactedDownloadState state)
    {
        var interruptedId = con.ExecuteScalar<long>("""
            INSERT INTO InterruptedDownload (Added)
            VALUES (datetime('now'))
            RETURNING Id
            """);

        foreach (var contentId in state.DownloadedContentEntries)
        {
            con.Execute("""
                INSERT INTO InterruptedDownloadContent(InterruptedDownloadId, ContentId)
                VALUES (@DownloadId, @ContentId)
                """,
                new { DownloadId = interruptedId, ContentId = contentId });
        }
    }

    private static void ClearIncompleteTransactedState(SqliteConnection con, TransactedDownloadState state)
    {
        if (state.MadeContentVersion is { } contentVersion)
        {
            con.Execute("DELETE FROM ContentVersion WHERE Id = @Id", new { Id = contentVersion });
        }
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
        TransactedDownloadState state,
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

        // Store the created version ID so we can manually delete it later if necessary.
        state.MadeContentVersion = versionId;

        // TODO: Download URL
        byte[] manifestHash;
        if (!string.IsNullOrEmpty(buildInfo.ManifestUrl)
            && !string.IsNullOrEmpty(buildInfo.ManifestDownloadUrl)
            && !string.IsNullOrEmpty(buildInfo.ManifestHash))
        {
            manifestHash = await ManifestDownloadNewVersion(buildInfo, con, versionId, state, cancel);
        }
        else if (buildInfo.DownloadUrl != null)
        {
            manifestHash = await ZipDownloadNewVersion(buildInfo, con, versionId, cancel);
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

    private static void EnsureBuffer(ref byte[] buf, int needsFit)
    {
        if (buf.Length >= needsFit)
            return;

        var newLen = 2 << BitOperations.Log2((uint)needsFit - 1);

        buf = new byte[newLen];
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

    private sealed class TransactedDownloadState
    {
        public readonly List<long> DownloadedContentEntries = [];
        public long? MadeContentVersion;
    }
}
