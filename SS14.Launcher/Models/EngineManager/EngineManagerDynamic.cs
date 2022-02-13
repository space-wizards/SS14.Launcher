using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using NSec.Cryptography;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.EngineManager;

/// <summary>
///     Downloads engine versions from the website.
/// </summary>
public sealed class EngineManagerDynamic : IEngineManager
{
    private readonly DataManager _cfg;
    private readonly HttpClient _http;

    public EngineManagerDynamic()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    public string GetEnginePath(string engineVersion)
    {
        if (!_cfg.EngineInstallations.Lookup(engineVersion).HasValue)
        {
            throw new ArgumentException("We do not have that engine version!");
        }

        return Path.Combine(LauncherPaths.DirEngineInstallations, $"{engineVersion}.zip");
    }

    public string GetEngineModule(string moduleName, string moduleVersion)
    {
        return Path.Combine(LauncherPaths.DirModuleInstallations, moduleName, moduleVersion);
    }

    public string GetEngineSignature(string engineVersion)
    {
        return _cfg.EngineInstallations.Lookup(engineVersion).Value.Signature;
    }

    public async Task<bool> DownloadEngineIfNecessary(
        string engineVersion,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
        if (_cfg.EngineInstallations.Lookup(engineVersion).HasValue)
        {
            // Already have the engine version, we're good.
            return false;
        }

        Log.Information("Installing engine version {version}...", engineVersion);

        Log.Debug("Loading manifest from {manifestUrl}...", ConfigConstants.RobustBuildsManifest);
        var manifest =
            await _http.GetFromJsonAsync<Dictionary<string, VersionInfo>>(
                ConfigConstants.RobustBuildsManifest, cancellationToken: cancel);

        if (!manifest!.TryGetValue(engineVersion, out var versionInfo))
        {
            throw new UpdateException("Unable to find engine version in manifest!");
        }

        if (versionInfo.Insecure)
        {
            throw new UpdateException("Specified engine version is insecure!");
        }

        var bestRid = RidUtility.FindBestRid(versionInfo.Platforms.Keys);
        if (bestRid == null)
        {
            throw new UpdateException("No engine version available for our platform!");
        }

        Log.Debug("Selecting RID {rid}", bestRid);

        var buildInfo = versionInfo.Platforms[bestRid];

        Log.Debug("Downloading engine: {EngineDownloadUrl}", buildInfo.Url);

        Helpers.EnsureDirectoryExists(LauncherPaths.DirEngineInstallations);

        var downloadTarget = Path.Combine(LauncherPaths.DirEngineInstallations, $"{engineVersion}.zip");
        await using var file = File.Create(downloadTarget, 4096, FileOptions.Asynchronous);

        try
        {
            await _http.DownloadToStream(buildInfo.Url, file, progress, cancel: cancel);
        }
        catch (OperationCanceledException)
        {
            // Don't leave behind garbage.
            await file.DisposeAsync();
            File.Delete(downloadTarget);

            throw;
        }

        _cfg.AddEngineInstallation(new InstalledEngineVersion(engineVersion, buildInfo.Signature));
        _cfg.CommitConfig();
        return true;
    }

    public async Task<bool> DownloadModuleIfNecessary(
        string moduleName,
        string moduleVersion,
        EngineModuleManifest manifest,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
        // Currently the module handling code assumes all modules need straight extract to disk.
        // This works for CEF, but who knows what the future might hold?

        Log.Debug("Checking to download {ModuleName} {ModuleVersion}", moduleName, moduleVersion);

        var versionData = manifest.Modules[moduleName].Versions[moduleVersion];

        Log.Debug("Selected module {ModuleName} {ModuleVersion}", moduleName, moduleVersion);

        var alreadyInstalled = _cfg.EngineModules.Any(m => m.Name == moduleName && m.Version == moduleVersion);

        if (alreadyInstalled)
        {
            Log.Debug("Already have module installed!");
            return false;
        }

        Log.Information("Installing {ModuleName} {ModuleVersion}", moduleName, moduleVersion);

        var bestRid = RidUtility.FindBestRid(versionData.Platforms.Keys);
        if (bestRid == null)
            throw new UpdateException("No module version available for our platform!");

        Log.Debug("Selecting RID {Rid}", bestRid);

        var platformData = versionData.Platforms[bestRid];

        Log.Debug("Downloading module: {EngineDownloadUrl}", platformData.Url);

        var moduleDiskPath = Path.Combine(LauncherPaths.DirModuleInstallations, moduleName);
        var moduleVersionDiskPath = Path.Combine(moduleDiskPath, moduleVersion);

        await Task.Run(() =>
        {
            // Avoid disk IO hang.
            Helpers.EnsureDirectoryExists(moduleDiskPath);
            Helpers.EnsureDirectoryExists(moduleVersionDiskPath);
            Helpers.ClearDirectory(moduleVersionDiskPath);
        }, CancellationToken.None);

        {
            await using var tempFile = TempFile.CreateTempFile();
            Log.Debug("Downloading into temp file: {TempFilePath}", tempFile.Name);

            await _http.DownloadToStream(platformData.Url, tempFile, progress, cancel);

            // Verify signature.
            tempFile.Seek(0, SeekOrigin.Begin);

            if (!VerifyModuleSignature(tempFile, platformData.Sig))
            {
#if DEBUG
                if (_cfg.GetCVar(CVars.DisableSigning))
                {
                    Log.Debug("Signature check failed for module, ignoring because signing disabled");
                }
                else
#endif
                {
                    throw new UpdateException("Failed to verify module signature!");
                }
            }

            // Done downloading, extract...
            Log.Debug("Download complete, extracting into: {TempFilePath}", moduleVersionDiskPath);

            tempFile.Seek(0, SeekOrigin.Begin);

            // CEF is so horrifically huge I'm enabling disk compression on it.
            Helpers.MarkDirectoryCompress(moduleVersionDiskPath);

            Helpers.ExtractZipToDirectory(moduleVersionDiskPath, tempFile);

            // Chmod required files.
            if (OperatingSystem.IsLinux())
            {
                switch (moduleName)
                {
                    case "Robust.Client.WebView":
                        Helpers.ChmodPlusX(Path.Combine(moduleVersionDiskPath, "Robust.Client.WebView"));
                        break;
                }
            }
        }

        _cfg.AddEngineModule(new InstalledEngineModule(moduleName, moduleVersion));
        _cfg.CommitConfig();

        Log.Debug("Done installing module!");

        return true;
    }

    private static unsafe bool VerifyModuleSignature(FileStream stream, string signature)
    {
        if (stream.Length > int.MaxValue)
            throw new InvalidOperationException("Unable to handle files larger than 2 GiB");

        // Use memory-mapped file here so we don't have to read the whole thing in at once.
        using var memoryMapped = MemoryMappedFile.CreateFromFile(
            stream,
            null,
            0,
            MemoryMappedFileAccess.Read,
            HandleInheritability.None,
            leaveOpen: true);

        using var accessor = memoryMapped.CreateViewAccessor(0, stream.Length, MemoryMappedFileAccess.Read);
        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

        try
        {
            var span = new ReadOnlySpan<byte>(ptr, (int)stream.Length);

            var pubKey = PublicKey.Import(
                SignatureAlgorithm.Ed25519,
                File.ReadAllBytes(LauncherPaths.PathPublicKey),
                KeyBlobFormat.PkixPublicKeyText);

            var sigBytes = Convert.FromHexString(signature);

            return SignatureAlgorithm.Ed25519.Verify(pubKey, span, sigBytes);
        }
        finally
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    public async Task<EngineModuleManifest> GetEngineModuleManifest(CancellationToken cancel = default)
    {
        return await _http.GetFromJsonAsync<EngineModuleManifest>(ConfigConstants.RobustModulesManifest, cancel) ??
               throw new InvalidDataException();
    }

    public async Task DoEngineCullMaybeAsync(SqliteConnection contenCon)
    {
        Log.Debug("Checking to cull engine dependencies");

        // Cull main engine installations.

        var modulesUsed = contenCon
            .Query<(string, string)>("SELECT DISTINCT ModuleName, ModuleVersion FROM ContentEngineDependency")
            .ToHashSet();

        var toCull = _cfg.EngineInstallations.Items.Where(i => !modulesUsed.Contains(("Robust", i.Version))).ToArray();

        foreach (var installation in toCull)
        {
            Log.Debug("Culling unused version {engineVersion}", installation.Version);

            var path = GetEnginePath(installation.Version);

            _cfg.RemoveEngineInstallation(installation);

            await Task.Run(() => File.Delete(path));
        }

        // Cull modules
        var toCullModules = _cfg.EngineModules.Where(m => !modulesUsed.Contains((m.Name, m.Version))).ToArray();

        foreach (var module in toCullModules)
        {
            Log.Debug("Culling unused module {EngineModule}", module);

            var path = GetEngineModule(module.Name, module.Version);

            _cfg.RemoveEngineModule(module);

            await Task.Run(() => Directory.Delete(path, true));
        }
    }

    public void ClearAllEngines()
    {
        foreach (var install in _cfg.EngineInstallations.Items.ToArray())
        {
            _cfg.RemoveEngineInstallation(install);
        }

        foreach (var module in _cfg.EngineModules.ToArray())
        {
            _cfg.RemoveEngineModule(module);
        }

        foreach (var file in Directory.EnumerateFiles(LauncherPaths.DirEngineInstallations))
        {
            File.Delete(file);
        }

        foreach (var dir in Directory.EnumerateFiles(LauncherPaths.DirModuleInstallations))
        {
            Directory.Delete(dir, recursive: true);
        }

        _cfg.CommitConfig();
    }

    private sealed class VersionInfo
    {
        [JsonInclude] [JsonPropertyName("insecure")]
#pragma warning disable CS0649
        public bool Insecure;
#pragma warning restore CS0649

        [JsonInclude] [JsonPropertyName("platforms")]
        public Dictionary<string, BuildInfo> Platforms = default!;
    }

    private sealed class BuildInfo
    {
        [JsonInclude] [JsonPropertyName("url")]
        public string Url = default!;

        [JsonInclude] [JsonPropertyName("sha256")]
        public string Sha256 = default!;

        [JsonInclude] [JsonPropertyName("sig")]
        public string Signature = default!;
    }
}
