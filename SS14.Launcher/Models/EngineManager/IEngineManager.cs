using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SS14.Launcher.Models.EngineManager;
// This is an interface instead of a class because
// I was originally planning to make Steam builds bundle the engine with the Steam download.

/// <summary>
///     Manages engine installations.
/// </summary>
public interface IEngineManager
{
    string GetEnginePath(string engineVersion);
    string GetEngineSignature(string engineVersion);

    Task<EngineModuleManifest> GetEngineModuleManifest(CancellationToken cancel = default);

    Task<EngineInstallationResult> DownloadEngineIfNecessary(
        string engineVersion,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default);

    /// <returns>True if something new had to be installed.</returns>
    Task<bool> DownloadModuleIfNecessary(
        string moduleName,
        string engineVersion,
        EngineModuleManifest manifest,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default);

    Task DoEngineCullMaybeAsync(SqliteConnection contenCon);
    void ClearAllEngines();
    string GetEngineModule(string moduleName, string moduleVersion);

    static string ResolveEngineModuleVersion(EngineModuleManifest manifest, string moduleName, string engineVersion)
    {
        if (!manifest.Modules.TryGetValue(moduleName, out var moduleData))
            throw new UpdateException("Unable to find engine module in manifest!");

        // Because engine modules are solely identified by *minimum* version,
        // we have to double-check that there isn't a newer version of the module available for the relevant engine.
        var engineVersionObj = Version.Parse(engineVersion);
        var selectedVersion = moduleData.Versions
            .Select(kv => new { Version = Version.Parse(kv.Key), kv.Key, kv.Value })
            .Where(kv => engineVersionObj >= kv.Version)
            .MaxBy(kv => kv.Version);

        if (selectedVersion == null)
            throw new UpdateException("Unable to find suitable module version in manifest!");

        // Follow redirects. The redirect target does not need to satisfy the engine minimum check above
        var key = selectedVersion.Key;
        var versionData = selectedVersion.Value;
        var seen = new HashSet<string> { key };
        while (versionData.Redirect is { } redirect)
        {
            if (!seen.Add(redirect))
                throw new UpdateException("Module version redirect loop detected in manifest!");

            if (!moduleData.Versions.TryGetValue(redirect, out var redirected))
                throw new UpdateException("Module version redirect target not found in manifest!");

            key = redirect;
            versionData = redirected;
        }

        return key;
    }
}

public record struct EngineInstallationResult(string Version, bool Changed);

public sealed record EngineModuleManifest(
    Dictionary<string, EngineModuleData> Modules
);

public sealed record EngineModuleData(
    Dictionary<string, EngineModuleVersionData> Versions
);

public sealed record EngineModuleVersionData(
    Dictionary<string, EngineModulePlatformData> Platforms,
    bool Insecure = false,
    [property: JsonPropertyName("redirect")]
    string? Redirect = null
);

public sealed record EngineModulePlatformData(
    string Url,
    string Sha256,
    string Sig
);
