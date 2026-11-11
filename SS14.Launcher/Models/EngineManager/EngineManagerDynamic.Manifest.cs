using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.EngineManager;

public sealed partial class EngineManagerDynamic
{
    // This part of the code is responsible for downloading and caching the Robust build manifest.

    // Matches the serializer options that HttpClientJsonExtensions (and thus GetFromJsonAsync) uses.
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _manifestSemaphore = new(1);
    private readonly Stopwatch _manifestStopwatch = Stopwatch.StartNew();

    private Dictionary<string, VersionInfo>? _cachedRobustVersionInfo;
    private TimeSpan _robustCacheValidUntil;

    /// <summary>
    /// Look up information about an engine version.
    /// </summary>
    /// <param name="version">The version number to look up.</param>
    /// <param name="followRedirects">Follow redirections in version info.</param>
    /// <param name="cancel">Cancellation token.</param>
    /// <returns>
    /// Information about the version, or null if it could not be found.
    /// The returned version may be different than what was requested if redirects were followed.
    /// </returns>
    private async ValueTask<FoundVersionInfo?> GetVersionInfo(
        string version,
        bool followRedirects = true,
        CancellationToken cancel = default)
    {
        await _manifestSemaphore.WaitAsync(cancel);
        try
        {
            return await GetVersionInfoCore(version, followRedirects, cancel);
        }
        finally
        {
            _manifestSemaphore.Release();
        }
    }

    private async ValueTask<FoundVersionInfo?> GetVersionInfoCore(
        string version,
        bool followRedirects,
        CancellationToken cancel)
    {
        // If we have a cached copy, and it's not expired, we check it.
        if (_cachedRobustVersionInfo != null && _robustCacheValidUntil > _manifestStopwatch.Elapsed)
        {
            // Check the version. If this fails, we immediately re-request the manifest as it may have changed.
            // (Connecting to a freshly-updated server with a new Robust version, within the cache window.)
            if (FindVersionInfoInCached(version, followRedirects) is { } foundVersionInfo)
                return foundVersionInfo;
        }

        await UpdateBuildManifest(cancel);

        return FindVersionInfoInCached(version, followRedirects);
    }

    private async Task UpdateBuildManifest(CancellationToken cancel)
    {
        // TODO: If-Modified-Since and If-None-Match request conditions.

        Log.Debug("Loading manifest from {manifestUrl}...", ConfigConstants.RobustBuildsManifest);
        var json = await FetchManifestJsonWithDiskFallback(
            ConfigConstants.RobustBuildsManifest,
            LauncherPaths.PathEngineBuildManifest,
            cancel);

        _cachedRobustVersionInfo = DeserializeManifest<Dictionary<string, VersionInfo>>(json);
        _robustCacheValidUntil = _manifestStopwatch.Elapsed + ConfigConstants.RobustManifestCacheTime;
    }

    /// <summary>
    ///     Fetches a manifest as raw JSON, persisting a copy to disk so it can be used as a
    ///     fallback if the CDN is unreachable on a later launch (e.g. blocked network regions).
    /// </summary>
    private async Task<string> FetchManifestJsonWithDiskFallback(
        UrlFallbackSet manifestUrl,
        string cachePath,
        CancellationToken cancel)
    {
        try
        {
            var json = await manifestUrl.GetStringAsync(_http, cancel);

            // Persist the raw JSON so the fallback copy is byte-identical to what we downloaded.
            SaveManifestToDisk(cachePath, json);

            return json;
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // The user cancelled the connection attempt; don't paper over it with the disk cache.
            throw;
        }
        catch (Exception e)
        {
            // All manifest URLs were unreachable. Fall back to the copy persisted to disk by a
            // previous successful download, if we have one.
            var cached = LoadManifestFromDisk(cachePath);
            if (cached == null)
                throw;

            Log.Warning(e, "Failed to download manifest, using cached copy from disk instead");
            return cached;
        }
    }

    private static T DeserializeManifest<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, ManifestJsonOptions)
               ?? throw new JsonException($"Manifest of type {typeof(T)} deserialized to null");
    }

    private static void SaveManifestToDisk(string path, string json)
    {
        try
        {
            Helpers.EnsureDirectoryExists(Path.GetDirectoryName(path)!);

            // Write to a temporary file first so a crash mid-write can't leave a corrupted cache.
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }
        }
        catch (Exception e)
        {
            // Failing to persist the cache must never break the connection flow.
            Log.Warning(e, "Failed to persist manifest cache to {Path}", path);
        }
    }

    private static string? LoadManifestFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            return File.ReadAllText(path);
        }
        catch (Exception e)
        {
            // Failing to read the cache must never break the connection flow.
            Log.Warning(e, "Failed to read manifest cache from {Path}", path);
            return null;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup of a temporary file; ignore failures.
        }
    }

    private FoundVersionInfo? FindVersionInfoInCached(string version, bool followRedirects)
    {
        Debug.Assert(_cachedRobustVersionInfo != null);

        if (!_cachedRobustVersionInfo.TryGetValue(version, out var versionInfo))
            return null;

        if (followRedirects)
        {
            while (versionInfo.RedirectVersion != null)
            {
                version = versionInfo.RedirectVersion;
                versionInfo = _cachedRobustVersionInfo[versionInfo.RedirectVersion];
            }
        }

        return new FoundVersionInfo(version, versionInfo);
    }

    private sealed record FoundVersionInfo(string Version, VersionInfo Info);

    private sealed record VersionInfo(
        bool Insecure,
        [property: JsonPropertyName("redirect")]
        string? RedirectVersion,
        Dictionary<string, BuildInfo> Platforms);

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
