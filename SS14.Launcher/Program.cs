using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.ReactiveUI;
using Serilog;
using Splat;
using SS14.Launcher.Models;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.ViewModels;
using SS14.Launcher.Views;
using LogEventLevel = Serilog.Events.LogEventLevel;

namespace SS14.Launcher;

internal static class Program
{
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

        VcRedistCheck.Check();
        var cfg = new DataManager();
        cfg.Load();
        Locator.CurrentMutable.RegisterConstant(cfg);

        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(LauncherVersion.Name, LauncherVersion.Version?.ToString()));
        http.DefaultRequestHeaders.Add("SS14-Launcher-Fingerprint", cfg.Fingerprint.ToString());
        Locator.CurrentMutable.RegisterConstant(http);

        LauncherPaths.CreateDirs();

        var logCfg = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console();

        if (cfg.LogLauncher)
        {
            logCfg = logCfg.WriteTo.File(LauncherPaths.PathLauncherLog);
        }

        Log.Logger = logCfg.CreateLogger();

#if DEBUG
        Logger.Sink = new AvaloniaSeriLogger(new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Area}] {Message} ({SourceType} #{SourceHash})\n")
            .CreateLogger());
#endif

        try
        {
            BuildAvaloniaApp().Start(AppMain, args);
        }
        finally
        {
            msgr.ShutdownPipeServer();
        }
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
        var msgr = Locator.Current.GetService<LauncherMessaging>();
        Locator.CurrentMutable.RegisterConstant<IEngineManager>(new EngineManagerDynamic());
        Locator.CurrentMutable.RegisterConstant(new ServerStatusCache());
        Locator.CurrentMutable.RegisterConstant(new Updater());
        Locator.CurrentMutable.RegisterConstant(new AuthApi());
        var lm = new LoginManager();
        Locator.CurrentMutable.RegisterConstant(lm);

        var viewModel = new MainWindowViewModel();
        var window = new MainWindow
        {
            DataContext = viewModel
        };
        viewModel.OnWindowInitialized();

        var lc = new LauncherCommands(viewModel);
        Locator.CurrentMutable.RegisterConstant(lc);
        msgr.ServerTask(lc);

        app.Run(window);
    }
}
