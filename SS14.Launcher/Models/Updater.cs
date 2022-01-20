using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
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

public sealed class Updater : ReactiveObject
{
    private static readonly IDeserializer ResourceManifestDeserializer =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    private readonly DataManager _cfg;
    private readonly ContentManager _content;
    private readonly IEngineManager _engineManager;
    private readonly HttpClient _http;
    private bool _updating;

    public Updater()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
        _content = Locator.Current.GetRequiredService<ContentManager>();
    }

    [Reactive] public UpdateStatus Status { get; private set; }
    [Reactive] public (long downloaded, long total)? Progress { get; private set; }

    public async Task<long?> RunUpdateForLaunchAsync(
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
            var install = await RunUpdate(buildInformation, cancel);
            Status = UpdateStatus.Ready;
            return install;
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

    private async Task<long> RunUpdate(
        ServerBuildInformation buildInformation,
        CancellationToken cancel)
    {
        Status = UpdateStatus.CheckingClientUpdate;

        EngineModuleManifest? moduleManifest = null;

        var con = _content.Connection;
        // Check if we already have this version installed in the content DB.
        var forkInfo = new { buildInformation.ForkId, buildInformation.Version };
        var existingVersion = con.QueryFirstOrDefault<ContentVersion>(
            "SELECT * FROM ContentVersion WHERE ForkId = @ForkId AND ForkVersion = @Version",
            forkInfo);

        // If server specifies zip hash, we need to make sure it matches the local version.
        var downloadNew = existingVersion == null;
        if (buildInformation.Hash != null)
        {
            if (existingVersion?.ZipHash == null
                || !existingVersion.ZipHash.AsSpan().SequenceEqual(Convert.FromHexString(buildInformation.Hash)))
            {
                downloadNew = true;
            }
        }

        long versionRowId;
        var engineVersion = buildInformation.EngineVersion;
        if (downloadNew)
        {
            // Temp file to download zip into.
            await using var tempFile = TempFile.CreateTempFile();

            var zipHash = await UpdateDownloadContent(tempFile, buildInformation, cancel);

            tempFile.Seek(0, SeekOrigin.Begin);

            // File downloaded, time to dump this into the DB.

            // ReSharper disable once UseAwaitUsing
            using var transaction = con.BeginTransaction();

            versionRowId = con.ExecuteScalar<long>(
                @"INSERT INTO ContentVersion(Hash, ForkId, ForkVersion, LastUsed, ZipHash)
                VALUES (zeroblob(32), @ForkId, @Version, datetime('now'), @ZipHash)
                RETURNING Id",
                new
                {
                    forkInfo.ForkId,
                    forkInfo.Version,
                    ZipHash = zipHash
                });

            var zip = new ZipArchive(tempFile, ZipArchiveMode.Read, leaveOpen: true);

            var hasher = SHA256.Create();

            foreach (var entry in zip.Entries)
            {
                if (entry.Name == "")
                    continue;

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
                    var compress = entry.Length - entry.CompressedLength > 10;
                    if (compress)
                    {
                        var ms = new MemoryStream();
                        var compressor = new DeflateStream(ms, CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        entryStream.CopyTo(compressor);
                        compressor.Dispose();

                        row = con.ExecuteScalar<long>(
                            @"INSERT INTO Content(Hash, Size, Compression, Data)
                        VALUES (@Hash, @Size, @Compression, @Blob)
                        RETURNING Id",
                            new
                            {
                                Hash = hash,
                                Size = entry.Length,
                                Blob = ms.ToArray(),
                                Compression = compress ? 1 : 0
                            });
                    }
                    else
                    {
                        row = con.ExecuteScalar<long>(
                            @"INSERT INTO Content(Hash, Size, Compression, Data)
                        VALUES (@Hash, @Size, @Compression, zeroblob(@Size))
                        RETURNING Id",
                            new { Hash = hash, Size = entry.Length, Compression = compress ? 1 : 0 });

                        using var stream = entry.Open();
                        using var blob = new SqliteBlob(con, "Content", "Data", row);

                        stream.CopyTo(blob);
                    }
                }

                con.Execute(
                    "INSERT INTO ContentManifest(VersionId, Path, ContentId) VALUES (@VersionId, @Path, @ContentId)",
                    new
                    {
                        VersionId = versionRowId,
                        Path = entry.FullName,
                        ContentId = row,
                    });
            }

            // Insert engine dependencies.

            // Engine version.
            con.Execute(
                @"INSERT INTO ContentEngineDependency(VersionId, ModuleName, ModuleVersion)
                VALUES (@Version, 'Robust', @EngineVersion)",
                new
                {
                    Version = versionRowId, EngineVersion = engineVersion
                });

            // Grab manifest file.
            var (manifestRowId, manifestCompression) = con.QueryFirstOrDefault<(long id, int compression)>(
                "SELECT c.ROWID, c.Compression FROM ContentManifest cm, Content c WHERE Path = 'manifest.yml' AND VersionId = @Version AND c.Id = cm.ContentId",
                new
                {
                    Version = versionRowId
                });

            if (manifestRowId != 0)
            {
                Stream stream = new SqliteBlob(con, "Content", "Data", manifestRowId, readOnly: true);
                if (manifestCompression == 1)
                    stream = new DeflateStream(stream, CompressionMode.Decompress);

                string[] modules;
                using (stream)
                {
                    modules = GetModuleNames(stream);
                }

                moduleManifest ??= await _engineManager.GetEngineModuleManifest(cancel);

                foreach (var module in modules)
                {
                    var version = IEngineManager.ResolveEngineModuleVersion(moduleManifest, module, engineVersion);

                    con.Execute(
                        @"INSERT INTO ContentEngineDependency(VersionId, ModuleName, ModuleVersion)
                        VALUES (@Version, @ModName, @EngineVersion)",
                        new
                        {
                            Version = versionRowId,
                            ModName = module,
                            ModVersion = version
                        });
                }
            }

            transaction.Commit();
        }
        else
        {
            versionRowId = existingVersion!.Id;

            // Ok so
            // The engine version, despite being stored in the ContentEngineDependency table,
            // is not actually based on the contents of the installed manifest.
            // Instead, it is provided to us by the server.
            // Because of this,
            // I'm just gonna update the existing entry to update the robust version to match the server.

            using var transaction = con.BeginTransaction();

            var version = new { Version = versionRowId };
            var curVersion = con.ExecuteScalar<string>(
                "SELECT ModuleVersion FROM ContentEngineDependency WHERE ModuleName = 'Robust' AND VersionId = @Version",
                version);
            if (curVersion != engineVersion)
            {
                con.Execute(
                    "UPDATE ContentEngineDependency SET ModuleVersion=@EngineVersion WHERE ModuleName = 'Robust' AND VersionId = @Version",
                    new { EngineVersion = engineVersion, Version = versionRowId });
            }

            // Also I already opened this transaction so uhhh let's update LastUsed too.
            con.Execute("UPDATE ContentVersion SET LastUsed = datetime('now') WHERE Id = @Version", version);

            transaction.Commit();
        }

        {
            Status = UpdateStatus.CheckingClientUpdate;
            var modules = con.Query<(string, string)>(
                "SELECT ModuleName, moduleVersion FROM ContentEngineDependency WHERE VersionId = @Version",
                new { Version = versionRowId });

            foreach (var (name, version) in modules)
            {
                if (name == "Robust")
                {
                    await InstallEngineVersionIfMissing(version, cancel);
                }
                else
                {
                    Status = UpdateStatus.DownloadingEngineModules;

                    moduleManifest ??= await _engineManager.GetEngineModuleManifest(cancel);
                }
            }
        }

        Status = UpdateStatus.CommittingDownload;

        // Should be no errors from here on out.

        /*if (changedContent || changedEngine)
        {
            Status = UpdateStatus.CullingEngine;
            await CullEngineVersionsMaybe();
        }*/

        _cfg.CommitConfig();

        Log.Information("Update done!");
        return versionRowId;
    }

    private async Task<byte[]> UpdateDownloadContent(
        Stream file,
        ServerBuildInformation buildInformation,
        CancellationToken cancel)
    {
        Status = UpdateStatus.DownloadingClientUpdate;

        Log.Information($"Downloading content update from {buildInformation.DownloadUrl}");

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

        if (buildInformation.Hash is { } expectHash)
        {
            var newFileHashString = Convert.ToHexString(hash);
            if (!expectHash.Equals(newFileHashString, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Hash mismatch. Expected: {expectHash}, got: {newFileHashString}");
            }
        }

        return hash;
    }

    private async Task CullEngineVersionsMaybe()
    {
        await _engineManager.DoEngineCullMaybeAsync();
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
        Dispatcher.UIThread.Post(() => Progress = (downloaded, total));
    }

    internal static byte[] HashFile(Stream stream)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(stream);
    }


    /*
    private bool CheckNeedUpdate(
        ServerBuildInformation buildInfo,
        [NotNullWhen(false)] out InstalledServerContent? installation)
    {
        var existingInstallation = _cfg.ServerContent.Lookup(buildInfo.ForkId);
        if (existingInstallation.HasValue)
        {
            installation = existingInstallation.Value;
            var currentVersion = existingInstallation.Value.CurrentVersion;
            if (buildInfo.Version != currentVersion)
            {
                Log.Information("Current version ({currentVersion}) is out of date, updating to {newVersion}.",
                    currentVersion, buildInfo.Version);

                return true;
            }

            // Check hash.
            var currentHash = existingInstallation.Value.CurrentHash;
            if (buildInfo.Hash != null && !buildInfo.Hash.Equals(currentHash, StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("Hash mismatch, re-downloading.");
                return true;
            }

            return false;
        }

        Log.Information("As it turns out, we don't have any version yet. Time to update.");

        installation = null;
        return true;
    }
    */

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
        CullingEngine,
        Ready,
        Error,
    }
}
