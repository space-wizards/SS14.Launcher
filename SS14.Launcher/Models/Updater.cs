using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
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

namespace SS14.Launcher.Models;

public sealed class Updater : ReactiveObject
{
    private readonly DataManager _cfg;
    private readonly IEngineManager _engineManager;
    private readonly HttpClient _http;
    private bool _updating;

    public Updater()
    {
        _cfg = Locator.Current.GetService<DataManager>();
        _engineManager = Locator.Current.GetService<IEngineManager>();
        _http = Locator.Current.GetService<HttpClient>();
    }

    [Reactive] public UpdateStatus Status { get; private set; }
    [Reactive] public (long downloaded, long total)? Progress { get; private set; }

    public async Task<InstalledServerContent?> RunUpdateForLaunchAsync(
        ServerBuildInformation buildInformation,
        CancellationToken cancel=default)
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

    private async Task<InstalledServerContent> RunUpdate(
        ServerBuildInformation buildInformation,
        CancellationToken cancel)
    {
        Status = UpdateStatus.CheckingClientUpdate;

        var changeEngine = await InstallEngineVersionIfMissing(buildInformation.EngineVersion, cancel);

        Status = UpdateStatus.CheckingClientUpdate;

        var (installation, changedContent) = await UpdateContentIfNecessary(buildInformation, cancel);

        if (changedContent || changeEngine)
        {
            Status = UpdateStatus.CullingEngine;
            await CullEngineVersionsMaybe();
        }

        Log.Information("Update done!");
        return installation;
    }

    private async Task<(InstalledServerContent, bool changed)> UpdateContentIfNecessary(
        ServerBuildInformation buildInformation,
        CancellationToken cancel)
    {
        if (!CheckNeedUpdate(buildInformation, out var existingInstallation))
        {
            return (existingInstallation, false);
        }

        Status = UpdateStatus.DownloadingClientUpdate;

        Log.Information($"Downloading content update from {buildInformation.DownloadUrl}");

        var diskId = existingInstallation?.DiskId ?? _cfg.GetNewInstallationId();
        var binPath = Path.Combine(LauncherPaths.DirServerContent,
            diskId.ToString(CultureInfo.InvariantCulture) + ".zip");

        Helpers.EnsureDirectoryExists(LauncherPaths.DirServerContent);
        await using var file = File.Create(binPath, 4096, FileOptions.Asynchronous);

        try
        {
            await _http.DownloadToStream(
                buildInformation.DownloadUrl,
                file,
                DownloadProgressCallback,
                cancel);
        }
        catch (OperationCanceledException)
        {
            // Don't leave behind garbage.
            await file.DisposeAsync();
            File.Delete(binPath);
            throw;
        }

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

        // Write version to disk.
        if (existingInstallation != null)
        {
            existingInstallation.CurrentVersion = buildInformation.Version;
            existingInstallation.CurrentHash = buildInformation.Hash;
            existingInstallation.CurrentEngineVersion = buildInformation.EngineVersion;
        }
        else
        {
            existingInstallation = new InstalledServerContent(
                buildInformation.Version,
                buildInformation.Hash,
                buildInformation.ForkId,
                diskId,
                buildInformation.EngineVersion);
            _cfg.AddInstallation(existingInstallation);
        }

        return (existingInstallation, true);
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

    public enum UpdateStatus
    {
        CheckingClientUpdate,
        DownloadingEngineVersion,
        DownloadingClientUpdate,
        Verifying,
        CullingEngine,
        Ready,
        Error,
    }
}
