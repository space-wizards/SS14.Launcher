using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using DynamicData;
using ReactiveUI;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models;

/// <summary>
/// Responsible for actually launching the game.
/// Either by connecting to a game server, or by launching a local content bundle.
/// </summary>
public class Connector : ReactiveObject
{
    private readonly Updater _updater;
    private readonly DataManager _cfg;
    private readonly LoginManager _loginManager;
    private readonly IEngineManager _engineManager;

    private ConnectionStatus _status = ConnectionStatus.None;
    private bool _clientExitedBadly;
    private readonly HttpClient _http;

    private TaskCompletionSource<PrivacyPolicyAcceptResult>? _acceptPrivacyPolicyTcs;
    private ServerPrivacyPolicyInfo? _serverPrivacyPolicyInfo;
    private bool _privacyPolicyDifferentVersion;

    public Connector()
    {
        _updater = Locator.Current.GetRequiredService<Updater>();
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _loginManager = Locator.Current.GetRequiredService<LoginManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    public ConnectionStatus Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public bool ClientExitedBadly
    {
        get => _clientExitedBadly;
        private set => this.RaiseAndSetIfChanged(ref _clientExitedBadly, value);
    }

    public ServerPrivacyPolicyInfo? PrivacyPolicyInfo => _serverPrivacyPolicyInfo;
    public bool PrivacyPolicyDifferentVersion
    {
        get => _privacyPolicyDifferentVersion;
        private set => this.RaiseAndSetIfChanged(ref _privacyPolicyDifferentVersion, value);
    }

    public async void Connect(string address, CancellationToken cancel = default)
    {
        try
        {
            await ConnectInternalAsync(address, cancel);
        }
        catch (ConnectException e)
        {
            Log.Error(e, "Failed to connect: {status}", e.Status);
            Status = e.Status;
        }
        catch (OperationCanceledException e)
        {
            Log.Information(e, "Cancelled connect");
            Status = ConnectionStatus.Cancelled;
        }
        finally
        {
            Cleanup();
        }
    }

    public async void LaunchContentBundle(IStorageFile file, CancellationToken cancel = default)
    {
        Log.Information("Launching content bundle: {FileName}", file.Path);

        try
        {
            await LaunchContentBundleInternal(file, cancel);
        }
        catch (ConnectException e)
        {
            Log.Error(e, "Failed to launch: {status}", e.Status);
            Status = e.Status;
        }
        catch (OperationCanceledException e)
        {
            Log.Information(e, "Cancelled launch");
            Status = ConnectionStatus.Cancelled;
        }
        finally
        {
            Cleanup();
        }
    }

    private async Task ConnectInternalAsync(string address, CancellationToken cancel)
    {
        Status = ConnectionStatus.Connecting;

        var (info, parsedAddr, infoAddr) = await GetServerInfoAsync(address, cancel);

        await HandlePrivacyPolicyAsync(info, cancel);

        // Run update.
        Status = ConnectionStatus.Updating;

        var installation = await RunUpdateAsync(info, cancel);

        var connectAddress = GetConnectAddress(info, infoAddr);

        await LaunchClientWrap(installation, info, info.BuildInformation, connectAddress, parsedAddr, false, cancel);
    }

    private async Task HandlePrivacyPolicyAsync(ServerInfo info, CancellationToken cancel)
    {
        if (info.PrivacyPolicy == null)
        {
            // Server has no privacy policy configured, nothing to do.
            return;
        }

        var identifier = info.PrivacyPolicy.Identifier;
        var version = info.PrivacyPolicy.Version;

        if (_cfg.HasAcceptedPrivacyPolicy(identifier, out var acceptedVersion))
        {
            if (version == acceptedVersion)
            {
                Log.Debug(
                    "User has previously accepted privacy policy {Identifier} with version {Version}",
                    identifier,
                    acceptedVersion);

                // User has previously accepted privacy policy, update last connected time in DB at least.
                _cfg.UpdateConnectedToPrivacyPolicy(identifier);
                _cfg.CommitConfig();
                return;
            }
            else
            {
                Log.Debug("User previously accepted privacy policy but version has changed!");
                PrivacyPolicyDifferentVersion = true;
            }
        }

        // Ask user for privacy policy acceptance by waiting here.
        Log.Debug("Prompting user for privacy policy acceptance: {Identifer} version {Version}", identifier, version);
        _serverPrivacyPolicyInfo = info.PrivacyPolicy;
        _acceptPrivacyPolicyTcs = new TaskCompletionSource<PrivacyPolicyAcceptResult>();

        Status = ConnectionStatus.AwaitingPrivacyPolicyAcceptance;
        var result = await _acceptPrivacyPolicyTcs.Task.WaitAsync(cancel);

        if (result == PrivacyPolicyAcceptResult.Accepted)
        {
            // Yippee they're ok with it.
            Log.Debug("User accepted privacy policy");
            _cfg.AcceptPrivacyPolicy(identifier, version);
            _cfg.CommitConfig();
            return;
        }

        // They're not ok with it. Just throw cancellation so the code cleans up I guess.
        // We could just have the connection screen treat "deny" as a cancellation op directly,
        // but that would make the logs less clear.
        Log.Information("User denied privacy policy, cancelling connection attempt!");
        throw new OperationCanceledException();
    }

    public void ConfirmPrivacyPolicy(PrivacyPolicyAcceptResult result)
    {
        if (_acceptPrivacyPolicyTcs == null)
        {
            Log.Error("_acceptPrivacyPolicyTcs is null???");
            return;
        }

        _acceptPrivacyPolicyTcs.SetResult(result);
    }

    private void Cleanup()
    {
        _serverPrivacyPolicyInfo = null;
        _acceptPrivacyPolicyTcs = null;
        PrivacyPolicyDifferentVersion = default;
    }

    private async Task LaunchContentBundleInternal(IStorageFile file, CancellationToken cancel)
    {
        Status = ConnectionStatus.Updating;

        ContentLaunchInfo installation;
        await using (var zipStream = await file.OpenReadAsync())
        {
            var zipHash = await Task.Run(() => Updater.HashFileSha256(zipStream), cancel);

            zipStream.Seek(0, SeekOrigin.Begin);

            using var zipFile = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var metadataJson = zipFile.GetEntry("rt_content_bundle.json");
            if (metadataJson == null)
            {
                Log.Error("Zip file did not contain rt_content_bundle.json");
                throw new ConnectException(ConnectionStatus.NotAContentBundle);
            }

            ContentBundleMetadata? metadata;
            using (var metadataStream = metadataJson.Open())
            {
                metadata = JsonSerializer.Deserialize<ContentBundleMetadata>(metadataStream);
            }

            if (metadata == null)
            {
                Log.Error("rt_content_bundle.json deserialized as null");
                throw new ConnectException(ConnectionStatus.NotAContentBundle);
            }

            Log.Debug("Loaded metadata for content bundle, continuing with launch");

            //
            // Big comment time
            //
            // Originally, I wanted to implement content bundles by not touching the Content DB at all.
            // (At least, if you're not using a base build)
            // The loader would open the zip file directly and provide the engine with both files simultaneously.
            //
            // That all kinda fell apart when I realized that manifest.yml has to be interpreted by the launcher.
            // And then also stuff like dependent engine versions have to be tracked and all that.
            // So, instead we merge the provided content bundle into the Content DB and start the game as normal.
            //
            // I don't like this solution much, as content bundles for SS14 replays will be quite bug (150+ MB).
            // It's a lot of data that needs to get uselessly shoved between the Content DB.
            //
            // In the future, a "hybrid" mode may be best:
            // The launcher will create a new version in the Content DB that contains just the manifest.yml.
            // (or base build data overlaid if necessary)
            // The loader would still be in charge of transparently merging in the zip file at runtime.
            //

            installation = await InstallContentBundleAsync(zipFile, zipHash, metadata, cancel);

            if (metadata.ServerGC == true)
                installation = installation with { ServerGC = true };
        }

        Log.Debug("Launching client");

        // I originally wanted to pass through build info,
        // but then realized I'd need to pipe the entries in the SQLite DB ("AnonymousContentBundle") up and ehhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh.
        await LaunchClientWrap(installation, null, null, null, null, true, cancel);
    }

    private async Task LaunchClientWrap(
        ContentLaunchInfo launchInfo,
        ServerInfo? info = null,
        ServerBuildInformation? buildInfo = null,
        Uri? connectAddress = null,
        Uri? parsedAddr = null,
        bool contentBundle = false,
        CancellationToken cancel = default)
    {
        Status = ConnectionStatus.StartingClient;

        var clientProc = await ConnectLaunchClient(launchInfo, info, buildInfo, connectAddress, parsedAddr, contentBundle);

        if (clientProc != null)
        {
            // Wait 300ms, if the client exits with a bad error code before that it's probably fucked.
            var waitClient = clientProc.WaitForExitAsync(cancel);
            var waitDelay = Task.Delay(300, cancel);

            await Task.WhenAny(waitDelay, waitClient);

            if (!clientProc.HasExited)
            {
                Status = ConnectionStatus.ClientRunning;
                await waitClient;
                return;
            }

            ClientExitedBadly = clientProc.ExitCode != 0;
        }
        else
        {
            ClientExitedBadly = true;
        }

        Status = ConnectionStatus.ClientExited;
    }

    private async Task<Process?> ConnectLaunchClient(ContentLaunchInfo launchInfo,
        ServerInfo? info,
        ServerBuildInformation? serverBuildInformation,
        Uri? connectAddress,
        Uri? parsedAddr,
        bool contentBundle)
    {
        var cVars = new List<(string, string)>();

        if (info != null && info.AuthInformation.Mode != AuthMode.Disabled && _loginManager.ActiveAccount != null)
        {
            var account = _loginManager.ActiveAccount;

            cVars.Add(("ROBUST_AUTH_TOKEN", account.LoginInfo.Token.Token));
            cVars.Add(("ROBUST_AUTH_USERID", account.LoginInfo.UserId.ToString()));
            cVars.Add(("ROBUST_AUTH_PUBKEY", info.AuthInformation.PublicKey));
            cVars.Add(("ROBUST_AUTH_SERVER", ConfigConstants.AuthUrl.GetMostSuccessfulUrl()));
        }

        try
        {
            var args = new List<string>
            {
                // Pass username to launched client.
                // We don't load username from client_config.toml when launched via launcher.
                "--username", _loginManager.ActiveAccount?.Username ?? ConfigConstants.FallbackUsername,

                // GLES2 forcing or using default fallback
                "--cvar", $"display.compat={_cfg.GetCVar(CVars.CompatMode) && !OperatingSystem.IsMacOS()}",

                // Tell game we are launcher
                "--cvar", "launch.launcher=true"
            };

            if (contentBundle)
            {
                args.Add("--cvar");
                args.Add("launch.content_bundle=true");
            }

            if (connectAddress != null)
            {
                // We are using the launcher. Don't show main menu etc..
                // Note: --launcher also implied --connect.
                // For this reason, content bundles do not set --launcher.
                args.Add("--launcher");

                args.Add("--connect-address");
                args.Add(connectAddress.ToString());
            }

            if (parsedAddr != null)
            {
                args.Add("--ss14-address");
                args.Add(parsedAddr.ToString());
            }

            // Pass build info to client. Initally added for replays, it is now used for connecting on modern robust CDN versions.
            // If engine_version or manifest_hash is null, the client WILL fail to connect.
            // serverBuildInformation is only null in case of content bundles which shouldn't try to connect to live servers anyways

            BuildCVar("download_url", serverBuildInformation?.DownloadUrl);
            BuildCVar("manifest_url", serverBuildInformation?.ManifestUrl);
            BuildCVar("manifest_download_url", serverBuildInformation?.ManifestDownloadUrl);
            BuildCVar("version", serverBuildInformation?.Version);
            BuildCVar("fork_id", serverBuildInformation?.ForkId);
            BuildCVar("hash", serverBuildInformation?.Hash);
            BuildCVar("manifest_hash", serverBuildInformation?.ManifestHash);
            BuildCVar("engine_version", serverBuildInformation?.EngineVersion);

            void BuildCVar(string name, string? value)
            {
                if (value == null)
                    return;

                args.Add("--cvar");
                args.Add($"build.{name}={value}");
            }

            // Launch client.
            return await LaunchClient(launchInfo, args, cVars);
        }
        catch (Exception e)
        {
            Log.Error(e, "Exception while starting client");
            return null;
        }
    }

    private static Uri GetConnectAddress(ServerInfo info, Uri infoAddr)
    {
        if (string.IsNullOrEmpty(info.ConnectAddress))
        {
            // No connect address specified, use same address/port as base address.
            return new UriBuilder
            {
                Scheme = "udp",
                Host = infoAddr.Host,
                Port = infoAddr.Port
            }.Uri;
        }

        try
        {
            return new Uri(info.ConnectAddress);
        }
        catch (FormatException e)
        {
            Log.Error(e, "Failed to parse ConnectAddress");
            throw new ConnectException(ConnectionStatus.ConnectionFailed);
        }
    }

    private async Task<ContentLaunchInfo> RunUpdateAsync(ServerInfo info, CancellationToken cancel)
    {
        // Must have been set when retrieving build info (inferred to be automatic zipping).
        Debug.Assert(info.BuildInformation != null, "info.BuildInformation != null");

        var installation = await _updater.RunUpdateForLaunchAsync(info.BuildInformation, cancel);
        if (installation == null)
        {
            throw new ConnectException(ConnectionStatus.UpdateError);
        }

        return installation;
    }

    private async Task<ContentLaunchInfo> InstallContentBundleAsync(
        ZipArchive archive,
        byte[] zipHash,
        ContentBundleMetadata metadata,
        CancellationToken cancel)
    {
        var installation = await _updater.InstallContentBundleForLaunchAsync(archive, zipHash, metadata, cancel);
        if (installation == null)
        {
            throw new ConnectException(ConnectionStatus.UpdateError);
        }

        return installation;
    }

    private async Task<(ServerInfo, Uri, Uri)> GetServerInfoAsync(string address, CancellationToken cancel)
    {
        if (!UriHelper.TryParseSs14Uri(address, out var parsedAddress))
        {
            Log.Error("Invalid URI in GetServerInfoAsync: {Uri}", address);
            throw new ConnectException(ConnectionStatus.ConnectionFailed);
        }

        // Fetch server connect info.
        var infoAddr = UriHelper.GetServerInfoAddress(parsedAddress);

        try
        {
            var info = await _http.GetFromJsonAsync<ServerInfo>(infoAddr, cancel) ?? throw new InvalidDataException();
            if (info.BuildInformation is {} buildInfo && (buildInfo.Acz || string.IsNullOrEmpty(buildInfo.DownloadUrl)))
            {
                var acz = info.BuildInformation.Acz;
                var apiAddress = UriHelper.GetServerApiAddress(parsedAddress);

                // Infer download URL to be self-hosted client address if not supplied
                // (The server may not know it's own address)
                info.BuildInformation.DownloadUrl = new Uri(apiAddress, "client.zip").ToString();

                if (acz)
                {
                    info.BuildInformation.ManifestUrl = new Uri(apiAddress, "manifest.txt").ToString();
                    info.BuildInformation.ManifestDownloadUrl = new Uri(apiAddress, "download").ToString();
                }
            }
            return (info, parsedAddress, infoAddr);
        }
        catch (Exception e) when (e is JsonException or HttpRequestException or InvalidDataException)
        {
            throw new ConnectException(ConnectionStatus.ConnectionFailed, e);
        }
    }

    public static InstalledEngineModule? GetInstalledModuleForEngineVersion(
        Version engineVersion,
        string moduleName,
        DataManager dataManager)
    {
        return dataManager.EngineModules
            .Where(m => m.Name == moduleName)
            .Select(m => new { Version = Version.Parse(m.Version), m })
            .Where(m => engineVersion >= m.Version)
            .MaxBy(m => m.Version)?.m;
    }

    private async Task<Process?> LaunchClient(
        ContentLaunchInfo launchInfo,
        IEnumerable<string> extraArgs,
        List<(string, string)> env)
    {
        var pubKey = LauncherPaths.PathPublicKey;
        var engineVersion = launchInfo.ModuleInfo.Single(x => x.Module == "Robust").Version;
        var binPath = _engineManager.GetEnginePath(engineVersion);
        var sig = _engineManager.GetEngineSignature(engineVersion);

        var startInfo = await GetLoaderStartInfo();

        startInfo.ArgumentList.Add(binPath);
        startInfo.ArgumentList.Add(sig);
        startInfo.ArgumentList.Add(pubKey);

        foreach (var (k, v) in env)
        {
            startInfo.EnvironmentVariables[k] = v;
        }

        EnvVar("SS14_LOADER_CONTENT_DB", LauncherPaths.PathContentDb);
        EnvVar("SS14_LOADER_CONTENT_VERSION", launchInfo.Version.ToString());

        // Env vars for engine modules.
        {
            foreach (var (moduleName, moduleVersion) in launchInfo.ModuleInfo)
            {
                if (moduleName == "Robust")
                    continue;

                var modulePath = _engineManager.GetEngineModule(moduleName, moduleVersion);

                var envVar = $"ROBUST_MODULE_{moduleName.ToUpperInvariant().Replace('.', '_')}";
                EnvVar(envVar, modulePath);
            }
        }

        if (_cfg.GetCVar(CVars.DisableSigning))
            EnvVar("SS14_DISABLE_SIGNING", "true");

        EnvVar("SS14_LAUNCHER_PATH", Process.GetCurrentProcess().MainModule!.FileName);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            EnvVar("SS14_LOG_CLIENT", LauncherPaths.PathClientMacLog);
        }

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        // Performance tweaks
        EnvVar("DOTNET_TieredPGO", "1");
        EnvVar("DOTNET_ReadyToRun", "0");

        if (launchInfo.ServerGC)
            EnvVar("DOTNET_gcServer", "1");

        ConfigureMultiWindow(launchInfo, startInfo);

        // DON'T ENABLE THIS THE LOADER USES THE LAUNCHER .NET VERSION ALWAYS SO ROLLFORWARD SHOULDN'T BE SPECIFIED.
        // DON'T KEEP FORGETTING THAT ABOVE LINE LIKE I DID.
        // EnvVar("DOTNET_ROLL_FORWARD", "Major");
        EnvVar("DOTNET_MULTILEVEL_LOOKUP", "0");

        startInfo.UseShellExecute = false;
        startInfo.ArgumentList.AddRange(extraArgs);

        /*
        foreach (var arg in startInfo.ArgumentList)
        {
            Log.Debug("arg: {Arg}", arg);
        }
        */

        var process = Process.Start(startInfo);

        if (process != null)
        {
            Log.Debug("Setting up manual-pipe logging for new client with PID {pid}.", process.Id);

            var fileStdout = new FileStream(
                LauncherPaths.PathClientStdoutLog,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Delete | FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous);

            var fileStderr = new FileStream(
                LauncherPaths.PathClientStderrLog,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Delete | FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous);

            PipeOutput(process, fileStdout, fileStderr);
        }

        return process;

        void EnvVar(string envVar, string? value)
        {
            startInfo.EnvironmentVariables[envVar] = value;
            // Log.Debug("Env: {EnvVar} = {Value}", envVar, value);
        }
    }

    private static void ConfigureMultiWindow(ContentLaunchInfo launchInfo, ProcessStartInfo startInfo)
    {
        // Implemented in private repo for Steam.
    }

    private static async void PipeOutput(Process process, Stream targetStdout, Stream targetStderr)
    {
        async Task DoPipe(StreamReader reader, Stream writer)
        {
            var readStream = reader.BaseStream;
            var buf = new byte[4096];
            while (true)
            {
                var read = await readStream.ReadAsync(buf);
                if (read == 0)
                {
                    Log.Debug("EOF, ending pipe logging for {pid}.", process.Id);
                    return;
                }

                await writer.WriteAsync(buf.AsMemory(0, read));
            }
        }

        await Task.WhenAll(
            DoPipe(process.StandardOutput, targetStdout),
            DoPipe(process.StandardError, targetStderr));
    }

    private static void PipeLogOutput(Process process)
    {
        Log.Debug("Piping output for process {pid} straight to logs", process.Id);

        async void DoPipe(TextReader reader)
        {
            while (true)
            {
                var read = await reader.ReadLineAsync();

                if (read == null)
                {
                    Log.Debug("EOF, ending pipe logging for {pid}", process.Id);
                    return;
                }

                Log.Information("piped: {content}", read);
            }
        }

        DoPipe(process.StandardError);
        DoPipe(process.StandardOutput);
    }

#pragma warning disable 162
    private static async Task<ProcessStartInfo> GetLoaderStartInfo()
    {
        string basePath;

#if FULL_RELEASE
            const bool release = true;
#else
        const bool release = false;
#endif

        if (release)
        {
            basePath = Path.Combine(LauncherPaths.DirLauncherInstall, "loader");
        }
        else
        {
#if RELEASE
            const string buildConfiguration = "Release";
#else
            const string buildConfiguration = "Debug";
#endif
            basePath = Path.GetFullPath(Path.Combine(
                LauncherPaths.DirLauncherInstall,
                "..", "..", "..", "..",
                "SS14.Loader", "bin", buildConfiguration, "net9.0"));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            return new ProcessStartInfo
            {
                FileName = Path.Combine(basePath, "SS14.Loader")
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo
            {
                FileName = Path.Combine(basePath, "SS14.Loader.exe"),
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (release)
            {
                var appPath = Path.Combine(basePath, "Space Station 14.app");
                Log.Debug("Using app bundle: {appPath}", appPath);

                Log.Debug("Clearing quarantine on loader.");

                // Clear the quarantine attribute off the loader to avoid any funny business with failing to start it.
                // This seemed to ONLY BE A PROBLEM if the quarantined file in question
                // is inside a secured location like ~/Desktop is now on Catalina.
                // Fucking stupid since we can clearly just work around it like this...
                // Thank you, Blaisorblade on Ask Different
                // https://apple.stackexchange.com/questions/105155/denied-file-read-access-on-file-i-own-and-have-full-r-w-permissions-on
                var xattr = Process.Start(new ProcessStartInfo
                {
                    FileName = "xattr",
                    ArgumentList = {"-d", "com.apple.quarantine", appPath},
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                });

                PipeLogOutput(xattr);

                await xattr.WaitForExitAsync();

                return new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = {appPath, "--args"},
                };
            }
            else
            {
                return new ProcessStartInfo
                {
                    FileName = Path.Combine(basePath, "SS14.Loader"),
                };
            }
        }

        throw new NotSupportedException("Unsupported platform.");
    }
#pragma warning restore 162

    public enum ConnectionStatus
    {
        None,
        Updating,
        UpdateError,
        Connecting,
        AwaitingPrivacyPolicyAcceptance,
        ConnectionFailed,
        StartingClient,
        ClientRunning,
        ClientExited,
        Cancelled,
        NotAContentBundle
    }

    private sealed class ConnectException : Exception
    {
        public ConnectionStatus Status { get; }

        public ConnectException(ConnectionStatus status)
        {
            Status = status;
        }

        public ConnectException(ConnectionStatus status, Exception inner)
            : base($"Failed to connect: {status}", inner)
        {
            Status = status;
        }
    }
}

public sealed record ContentBundleMetadata(
    [property: JsonPropertyName("server_gc")] bool? ServerGC,
    [property: JsonPropertyName("engine_version")] string EngineVersion,
    [property: JsonPropertyName("base_build")] ContentBundleBaseBuild? BaseBuild
);

public sealed record ContentBundleBaseBuild(
    [property: JsonPropertyName("fork_id")] string ForkId,
    [property: JsonPropertyName("version")] string Version,
    // Old zip-download system.
    [property: JsonPropertyName("download_url")] string? DownloadUrl,
    [property: JsonPropertyName("hash")] string? Hash,
    // Newer manifest download system.
    [property: JsonPropertyName("manifest_download_url")] string? ManifestDownloadUrl,
    [property: JsonPropertyName("manifest_url")] string? ManifestUrl,
    [property: JsonPropertyName("manifest_hash")] string? ManifestHash
);

public enum PrivacyPolicyAcceptResult
{
    Denied,
    Accepted,
}
