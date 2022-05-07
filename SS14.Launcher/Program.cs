using System;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.ReactiveUI;
using Serilog;
using Splat;
using SS14.Launcher.Models;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels;
using SS14.Launcher.Views;
using LogEventLevel = Serilog.Events.LogEventLevel;

namespace SS14.Launcher;

internal static class Program
{
    private static Task? _serverTask;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static void Main(string[] args)
    {
        var msgr = new LauncherMessaging();
        Locator.CurrentMutable.RegisterConstant(msgr);

        // Parse arguments as early as possible for launcher messaging reasons.
        string[] commands = {LauncherCommands.PingCommand};
        var commandSendAnyway = false;
        if (args.Length == 1)
        {
            // Check if this is a valid Uri, since that indicates re-invocation.
            if (Uri.TryCreate(args[0], UriKind.Absolute, out var result))
            {
                commands = new string[] {LauncherCommands.BlankReasonCommand, LauncherCommands.ConstructConnectCommand(result)};
                // This ensures we queue up the connection even if we're starting the launcher now.
                commandSendAnyway = true;
            }
        }
        else if (args.Length >= 2)
        {
            if (args[0] == "--commands")
            {
                // Trying to send an arbitrary series of commands.
                // This is how the Loader is expected to communicate (and start the launcher if necessary).
                // Note that there are special "untrusted text" versions of the commands that should be used.
                commands = new string[args.Length - 1];
                for (var i = 0; i < commands.Length; i++)
                    commands[i] = args[i + 1];
                commandSendAnyway = true;
            }
        }
        // Note: This MUST occur before we do certain actions like:
        // + Open the launcher log file (and therefore wipe a user's existing launcher log)
        // + Initialize Avalonia (and therefore waste whatever time it takes to do that)
        // Therefore any messages you receive at this point will be Console.WriteLine-only!
        if (msgr.SendCommandsOrClaim(commands, commandSendAnyway))
            return;

        var logCfg = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console();

        Log.Logger = logCfg.CreateLogger();

        VcRedistCheck.Check();
        LauncherPaths.CreateDirs();

        var cfg = new DataManager();
        cfg.Load();
        Locator.CurrentMutable.RegisterConstant(cfg);

        var http = HappyEyeballsHttp.CreateHttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(LauncherVersion.Name, LauncherVersion.Version?.ToString()));
        http.DefaultRequestHeaders.Add("SS14-Launcher-Fingerprint", cfg.Fingerprint.ToString());
        Locator.CurrentMutable.RegisterConstant(http);

        if (cfg.GetCVar(CVars.LogLauncher))
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(cfg.GetCVar(CVars.LogLauncherVerbose) ? LogEventLevel.Verbose : LogEventLevel.Debug)
                .WriteTo.Console()
                .WriteTo.File(LauncherPaths.PathLauncherLog)
                .CreateLogger();
        }

        Log.Information("Runtime: {RuntimeDesc} {RuntimeInfo}", RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
        Log.Information("OS: {OSDesc} {OSArch}", RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture);
        Log.Information("Launcher version: {LauncherVersion}", typeof(Program).Assembly.GetName().Version);

#if DEBUG
        Logger.Sink = new AvaloniaSeriLogger(new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Area}] {Message} ({SourceType} #{SourceHash})\n")
            .CreateLogger());
#endif

        try
        {
            using (msgr.PipeServerSelfDestruct)
            {
                BuildAvaloniaApp().Start(AppMain, args);
                msgr.PipeServerSelfDestruct.Cancel();
            }
        }
        finally
        {
            Log.CloseAndFlush();
            cfg.Close();
        }
        // Wait for pipe server to shut down cleanly.
        _serverTask?.Wait();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI();

    // Your application's entry point. Here you can initialize your MVVM framework, DI
    // container, etc.
    private static void AppMain(Application app, string[] args)
    {
        var cfg = Locator.Current.GetService<DataManager>();
        var msgr = Locator.Current.GetRequiredService<LauncherMessaging>();
        var contentManager = new ContentManager();
        Locator.CurrentMutable.RegisterConstant(contentManager);
        Locator.CurrentMutable.RegisterConstant<IEngineManager>(new EngineManagerDynamic());
        Locator.CurrentMutable.RegisterConstant(new Updater());
        Locator.CurrentMutable.RegisterConstant(new AuthApi());
        Locator.CurrentMutable.RegisterConstant(new ServerListCache());
        var lm = new LoginManager();
        Locator.CurrentMutable.RegisterConstant(lm);
        contentManager.Initialize();

        var viewModel = new MainWindowViewModel();
        var window = new MainWindow
        {
            DataContext = viewModel
        };
        viewModel.OnWindowInitialized();

        var lc = new LauncherCommands(viewModel);
        lc.RunCommandTask();
        Locator.CurrentMutable.RegisterConstant(lc);
        _serverTask = msgr.ServerTask(lc);

        app.Run(window);

        lc.Shutdown();
    }
}
