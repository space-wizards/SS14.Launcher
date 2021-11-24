using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using Newtonsoft.Json;
using ReactiveUI;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher.Models;

public class Connector : ReactiveObject
{
    private readonly Updater _updater;
    private readonly DataManager _cfg;
    private readonly LoginManager _loginManager;
    private readonly IEngineManager _engineManager;

    private ConnectionStatus _status = ConnectionStatus.None;
    private bool _clientExitedBadly;
    private readonly HttpClient _http;

    public Connector()
    {
        _updater = Locator.Current.GetService<Updater>();
        _cfg = Locator.Current.GetService<DataManager>();
        _loginManager = Locator.Current.GetService<LoginManager>();
        _engineManager = Locator.Current.GetService<IEngineManager>();
        _http = Locator.Current.GetService<HttpClient>();
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
    }

    private async Task ConnectInternalAsync(string address, CancellationToken cancel)
    {
        Status = ConnectionStatus.Connecting;

        var (info, parsedAddr, infoAddr) = await GetServerInfoAsync(address, cancel);

        // Run update.
        Status = ConnectionStatus.Updating;

        var installation = await RunUpdateAsync(info, cancel);

        var connectAddress = GetConnectAddress(info, infoAddr);

        Status = ConnectionStatus.StartingClient;

        var clientProc = await ConnectLaunchClient(info, installation, connectAddress, parsedAddr);

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

    private async Task<Process?> ConnectLaunchClient(ServerInfo info, InstalledServerContent installedServerContent,
        Uri connectAddress, Uri parsedAddr)
    {
        var cVars = new List<(string, string)>();

        if (info.AuthInformation.Mode != AuthMode.Disabled && _loginManager.ActiveAccount != null)
        {
            var account = _loginManager.ActiveAccount;

            cVars.Add(("ROBUST_AUTH_TOKEN", account.LoginInfo.Token.Token));
            cVars.Add(("ROBUST_AUTH_USERID", account.LoginInfo.UserId.ToString()));
            cVars.Add(("ROBUST_AUTH_PUBKEY", info.AuthInformation.PublicKey));
            cVars.Add(("ROBUST_AUTH_SERVER", ConfigConstants.AuthUrl));
        }

        try
        {
            // Launch client.
            return await LaunchClient(info.BuildInformation.EngineVersion, installedServerContent, new[]
            {
                // We are using the launcher. Don't show main menu etc..
                "--launcher",

                // Pass username to launched client.
                // We don't load username from client_config.toml when launched via launcher.
                "--username", _loginManager.ActiveAccount?.Username ?? "JoeGenero",

                // Connection address
                "--connect-address", connectAddress.ToString(),

                // ss14(s):// address passed in. Only used for feedback in the client.
                "--ss14-address", parsedAddr.ToString(),

                // GLES2 forcing or using default fallback
                "--cvar", $"display.compat={_cfg.ForceGLES2}",
            }, cVars);
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

    private async Task<InstalledServerContent> RunUpdateAsync(ServerInfo info, CancellationToken cancel)
    {
        var installation = await _updater.RunUpdateForLaunchAsync(info.BuildInformation, cancel);
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
            var resp = await _http.GetStringAsync(infoAddr, cancel);
            var info = JsonConvert.DeserializeObject<ServerInfo>(resp);
            if (info.BuildInformation != null)
            {
                // Infer download URL to be self-hosted client address if not supplied
                // (The server may not know it's own address)
                if (string.IsNullOrEmpty(info.BuildInformation.DownloadUrl))
                {
                    info.BuildInformation.DownloadUrl = UriHelper.GetServerSelfhostedClientZipAddress(parsedAddress).ToString();
                }
            }
            return (info, parsedAddress, infoAddr);
        }
        catch (Exception e) when (e is JsonException || e is HttpRequestException)
        {
            throw new ConnectException(ConnectionStatus.ConnectionFailed, e);
        }
    }

    private async Task<Process?> LaunchClient(
        string engineVersion,
        InstalledServerContent installedServerContent,
        IEnumerable<string> extraArgs,
        List<(string, string)> env)
    {
        var pubKey = Path.Combine(LauncherPaths.DirLauncherInstall, "signing_key");
        var binPath = _engineManager.GetEnginePath(engineVersion);
        var sig = _engineManager.GetEngineSignature(engineVersion);
        var contentPath = Path.Combine(LauncherPaths.DirServerContent,
            installedServerContent.DiskId.ToString(CultureInfo.InvariantCulture) + ".zip");

        var startInfo = await GetLoaderStartInfo();

        startInfo.ArgumentList.Add(binPath);
        startInfo.ArgumentList.Add(sig);
        startInfo.ArgumentList.Add(pubKey);

        startInfo.ArgumentList.Add("--mount-zip");
        startInfo.ArgumentList.Add(contentPath);

        foreach (var (k, v) in env)
        {
            startInfo.EnvironmentVariables[k] = v;
        }

        startInfo.EnvironmentVariables["DOTNET_ROLL_FORWARD"] = "LatestMajor";

        if (_cfg.DisableSigning)
            startInfo.EnvironmentVariables["SS14_DISABLE_SIGNING"] = "true";

        startInfo.EnvironmentVariables["SS14_LAUNCHER_PATH"] = Process.GetCurrentProcess().MainModule.FileName;

        // ReSharper disable once ReplaceWithSingleAssignment.False
        var manualPipeLogging = false;
        if (_cfg.LogClient)
        {
            manualPipeLogging = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                startInfo.Environment["SS14_LOG_CLIENT"] = LauncherPaths.PathClientMacLog;
            }

            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        if (_cfg.DynamicPGO)
        {
            Log.Debug("Dynamic PGO is enabled.");
            startInfo.EnvironmentVariables["DOTNET_TieredPGO"] = "1";
            startInfo.EnvironmentVariables["DOTNET_TC_QuickJitForLoops"] = "1";
            startInfo.EnvironmentVariables["DOTNET_ReadyToRun"] = "0";
        }

        startInfo.UseShellExecute = false;
        startInfo.ArgumentList.AddRange(extraArgs);
        var process = Process.Start(startInfo);

        if (manualPipeLogging && process != null)
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
    }

    private static async void PipeOutput(Process process, Stream targetStdout, Stream targetStderr)
    {
        await using var writerOut = new StreamWriter(targetStdout) {AutoFlush = true};
        await using var writerErr = new StreamWriter(targetStderr) {AutoFlush = true};

        async Task DoPipe(TextReader reader, TextWriter writer)
        {
            while (true)
            {
                var read = await reader.ReadLineAsync();

                if (read == null)
                {
                    Log.Debug("EOF, ending pipe logging for {pid}.", process.Id);
                    return;
                }

                await writer.WriteLineAsync(read);
            }
        }

        await Task.WhenAll(
            DoPipe(process.StandardOutput, writerOut),
            DoPipe(process.StandardError, writerErr));
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
            basePath = Path.GetFullPath(Path.Combine(
                LauncherPaths.DirLauncherInstall,
                "..", "..", "..", "..",
                "SS14.Loader", "bin", "Debug", "net6.0"));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
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
        ConnectionFailed,
        StartingClient,
        ClientRunning,
        ClientExited,
        Cancelled
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
