using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using Splat;
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
    private readonly IEngineManager _engineManager;
    private readonly HttpClient _http;
    private bool _updating;

    public Updater()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    [Reactive] public UpdateStatus Status { get; private set; }
    [Reactive] public (long downloaded, long total)? Progress { get; private set; }

    public async Task<int?> RunUpdateForLaunchAsync(
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

    private async Task<int> RunUpdate(
        ServerBuildInformation buildInformation,
        CancellationToken cancel)
    {
        // I tried to fit modules into this and it all fell apart.
        // Please bear with me, all of this is a mess.

        Status = UpdateStatus.CheckingClientUpdate;

        var changedEngine = await InstallEngineVersionIfMissing(buildInformation.EngineVersion, cancel);

        Status = UpdateStatus.CheckingClientUpdate;

        var changedContent = false;

        var diskId = 0;
        FileStream? file = null;
        var deleteTemp = true;
        InstalledServerContent? installation;
        try
        {
            if (CheckNeedUpdate(buildInformation, out installation))
            {
                changedContent = true;

                diskId = _cfg.GetNewInstallationId();
                var binPath = LauncherPaths.GetContentZip(diskId);

                Log.Debug("Downloading new content into {NewContentPath}", binPath);

                Helpers.EnsureDirectoryExists(LauncherPaths.DirServerContent);
                file = File.Create(binPath, 4096, FileOptions.Asynchronous);

                await UpdateDownloadContent(file, buildInformation, cancel);
            }
            else
            {
                deleteTemp = false;
                file = File.OpenRead(LauncherPaths.GetContentZip(installation.DiskId));
            }

            if (changedEngine || changedContent)
            {
                file.Position = 0;

                Status = UpdateStatus.CheckingEngineModules;

                // Check for any modules that need installing.
                var modules = GetModuleNames(file);
                if (modules.Length != 0)
                {
                    var moduleManifest = await _engineManager.GetEngineModuleManifest(cancel);

                    Status = UpdateStatus.DownloadingEngineModules;

                    foreach (var module in modules)
                    {
                        await _engineManager.DownloadModuleIfNecessary(
                            module,
                            buildInformation.EngineVersion,
                            moduleManifest,
                            DownloadProgressCallback, cancel);
                    }

                    Progress = null;
                }
            }
        }
        catch
        {
            // Dispose download target file if content download or module download failed.
            if (deleteTemp && file != null)
            {
                var name = file.Name;
                await file.DisposeAsync();
                File.Delete(name);
            }

            throw;
        }
        finally
        {
            if (file != null)
                await file.DisposeAsync();
        }

        Status = UpdateStatus.CommittingDownload;

        // Should be no errors from here on out.

        if (changedContent)
        {
            // Write version to disk.
            if (installation != null)
            {
                var prevId = installation.DiskId;
                var prevPath = LauncherPaths.GetContentZip(prevId);

                Log.Debug("Deleting old build: {PrevBuildPath}", prevPath);

                try
                {
                    File.Delete(prevPath);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to delete previous build!");
                }

                installation.CurrentVersion = buildInformation.Version;
                installation.CurrentHash = buildInformation.Hash;
                installation.CurrentEngineVersion = buildInformation.EngineVersion;
                installation.DiskId = diskId;
            }
            else
            {
                installation = new InstalledServerContent(
                    buildInformation.Version,
                    buildInformation.Hash,
                    buildInformation.ForkId,
                    diskId,
                    buildInformation.EngineVersion);

                _cfg.AddInstallation(installation);
            }

        }

        if (changedContent || changedEngine)
        {
            Status = UpdateStatus.CullingEngine;
            await CullEngineVersionsMaybe();
        }

        _cfg.CommitConfig();

        Log.Information("Update done!");
        return installation!;
    }

    private async Task UpdateDownloadContent(
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

        if (buildInformation.Hash != null)
        {
            var hash = await Task.Run(() => HashFile(file), cancel);
            file.Position = 0;

            var expectHash = buildInformation.Hash;

            var newFileHashString = Convert.ToHexString(hash);
            if (!expectHash.Equals(newFileHashString, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Hash mismatch. Expected: {expectHash}, got: {newFileHashString}");
            }
        }
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

    public static string[] GetModuleNames(Stream zipContent)
    {
        // Check zip file contents for manifest.yml and read the modules the server needs.

        using var zip = new ZipArchive(zipContent, ZipArchiveMode.Read, leaveOpen: true);

        var manifest = zip.GetEntry("manifest.yml");
        if (manifest != null)
        {
            using var streamReader = new StreamReader(manifest.Open());
            var manifestData = ResourceManifestDeserializer.Deserialize<ResourceManifestData?>(streamReader);
            if (manifestData != null)
                return manifestData.Modules;
        }

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
