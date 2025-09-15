using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
public sealed partial class EngineManagerDynamic : IEngineManager
{
    public const string OverrideVersionName = "_OVERRIDE_";

    private readonly DataManager _cfg;
    private readonly HttpClient _http;

    public EngineManagerDynamic()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    public string GetEnginePath(string engineVersion)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
        {
            return FindOverrideZip("Robust.Client", _cfg.GetCVar(CVars.EngineOverridePath));
        }
#endif

        if (!_cfg.EngineInstallations.Lookup(engineVersion).HasValue)
        {
            throw new ArgumentException("We do not have that engine version!");
        }

        return Path.Combine(LauncherPaths.DirEngineInstallations, $"{engineVersion}.zip");
    }

    public string GetEngineModule(string moduleName, string moduleVersion)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
            moduleVersion = OverrideVersionName;
#endif

        return Path.Combine(LauncherPaths.DirModuleInstallations, moduleName, moduleVersion);
    }

    public string GetEngineSignature(string engineVersion)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
            return "DEADBEEF";
#endif

        return _cfg.EngineInstallations.Lookup(engineVersion).Value.Signature;
    }

    public async Task<EngineInstallationResult> DownloadEngineIfNecessary(
        string engineVersion,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
        {
            // Engine override means we don't need to download anything, we have it locally!
            // At least, if we don't, we'll just blame the developer that enabled it.
            return new EngineInstallationResult(engineVersion, false);
        }
#endif

        var foundVersion = await GetVersionInfo(engineVersion, cancel: cancel);
        if (foundVersion == null)
            throw new UpdateException("Unable to find engine version in manifest!");

        if (foundVersion.Info.Insecure)
            throw new UpdateException("Specified engine version is insecure!");

        Log.Debug(
            "Requested engine version was {RequestedEngien}, redirected to {FoundVersion}",
            engineVersion,
            foundVersion.Version);

        if (_cfg.EngineInstallations.Lookup(foundVersion.Version).HasValue)
        {
            // Already have the engine version, we're good.
            return new EngineInstallationResult(foundVersion.Version, false);
        }

        Log.Information("Installing engine version {version}...", foundVersion.Version);

        var bestRid = RidUtility.FindBestRid(foundVersion.Info.Platforms.Keys);
        if (bestRid == null)
        {
            throw new NoEngineForPlatformException("No engine version available for our platform!");
        }

        Log.Debug("Selecting RID {rid}", bestRid);

        var buildInfo = foundVersion.Info.Platforms[bestRid];

        Log.Debug("Downloading engine: {EngineDownloadUrl}", buildInfo.Url);

        Helpers.EnsureDirectoryExists(LauncherPaths.DirEngineInstallations);

        var downloadTarget = Path.Combine(LauncherPaths.DirEngineInstallations, $"{foundVersion.Version}.zip");
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

        _cfg.AddEngineInstallation(new InstalledEngineVersion(foundVersion.Version, buildInfo.Signature));
        _cfg.CommitConfig();
        return new EngineInstallationResult(foundVersion.Version, true);
    }

    public async Task<bool> DownloadModuleIfNecessary(
        string moduleName,
        string moduleVersion,
        EngineModuleManifest manifest,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
        {
            // For modules we have to extract them from the zip to disk first.
            // So it's a little more involved than just giving a different zip path to the launch code.
            await CopyOverrideModule(moduleName);
            return true;
        }
#endif

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
            throw new NoModuleForPlatformException("No module version available for our platform!");

        Log.Debug("Selecting RID {Rid}", bestRid);

        var platformData = versionData.Platforms[bestRid];

        Log.Debug("Downloading module: {EngineDownloadUrl}", platformData.Url);

        GetModulePaths(
            moduleName,
            moduleVersion,
            out var moduleDiskPath,
            out var moduleVersionDiskPath);

        await ClearModuleDir(moduleDiskPath, moduleVersionDiskPath);

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

            ExtractModule(moduleName, moduleVersionDiskPath, tempFile);
        }

        _cfg.AddEngineModule(new InstalledEngineModule(moduleName, moduleVersion));
        _cfg.CommitConfig();

        Log.Debug("Done installing module!");

        return true;

    }

    private async Task CopyOverrideModule(string name)
    {
        GetModulePaths(
            name,
            OverrideVersionName,
            out var modPath,
            out var modVersionPath);

        await ClearModuleDir(modPath, modVersionPath);

        var zipPath = FindOverrideZip(name, _cfg.GetCVar(CVars.EngineOverridePath));
        using var zip = File.OpenRead(zipPath);

        // Note: not marking directory as compressed since it would take a while to start.
        ExtractModule(name, modVersionPath, zip);
    }

    private static void GetModulePaths(
        string module,
        string version,
        out string moduleDiskPath,
        out string moduleVersionDiskPath)
    {
        moduleDiskPath = Path.Combine(LauncherPaths.DirModuleInstallations, module);
        moduleVersionDiskPath = Path.Combine(moduleDiskPath, version);
    }

    private static async Task ClearModuleDir(string modDiskPath, string modVersionDiskPath)
    {
        await Task.Run(() =>
        {
            // Avoid disk IO hang.
            Helpers.EnsureDirectoryExists(modDiskPath);
            Helpers.EnsureDirectoryExists(modVersionDiskPath);
            Helpers.ClearDirectory(modVersionDiskPath);
        }, CancellationToken.None);
    }

    private static void ExtractModule(string moduleName, string moduleVersionDiskPath, FileStream tempFile)
    {
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
        return await ConfigConstants.RobustModulesManifest.GetFromJsonAsync<EngineModuleManifest>(_http, cancel) ??
               throw new InvalidDataException();
    }

    public async Task DoEngineCullMaybeAsync(SqliteConnection contenCon)
    {
        Log.Debug("Checking to cull engine dependencies");

        // Cull main engine installations.

        var origModulesUsed = contenCon
            .Query<(string, string)>("SELECT DISTINCT ModuleName, ModuleVersion FROM ContentEngineDependency")
            .ToList();

        // GOD DAMNIT more bodging everything together.
        // The code sucks.
        // My shitty hacks to do engine version redirection fall apart here as well.
        var modulesUsed = new HashSet<(string, string)>();
        foreach (var (name, version) in origModulesUsed)
        {
            if (name == "Robust" && await GetVersionInfo(version) is { } redirect)
            {
                modulesUsed.Add(("Robust", redirect.Version));
            }
            else
            {
                modulesUsed.Add((name, version));
            }
        }

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

    private static string FindOverrideZip(string name, string dir)
    {
        var foundRids = new List<string>();

        var regex = new Regex(@$"^{Regex.Escape(name)}_([a-z\-\d]+)\.zip$");
        foreach (var item in Directory.EnumerateFiles(dir))
        {
            var fileName = Path.GetFileName(item);
            var match = regex.Match(fileName);
            if (!match.Success)
                continue;

            foundRids.Add(match.Groups[1].Value);
        }

        var rid = RidUtility.FindBestRid(foundRids);
        if (rid == null)
            throw new UpdateException($"Unable to find overriden {name} for current platform");

        var path = Path.Combine(dir, $"{name}_{rid}.zip");
        Log.Warning("Using override for {Name}: {Path}", name, path);
        return path;
    }
}
