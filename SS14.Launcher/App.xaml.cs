using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using JetBrains.Annotations;
using Serilog;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.OverrideAssets;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels;
using SS14.Launcher.Views;

namespace SS14.Launcher;

public class App : Application
{
    private static readonly Dictionary<string, AssetDef> AssetDefs = new()
    {
        ["WindowIcon"] = new AssetDef("icon.ico", AssetType.WindowIcon),
        ["LogoLong"] = new AssetDef("logo-long.png", AssetType.Bitmap),
    };

    private readonly OverrideAssetsManager _overrideAssets;

    private readonly Dictionary<string, object> _baseAssets = new();

    // XAML insists on a parameterless constructor existing, despite this never being used.
    [UsedImplicitly]
    public App()
    {
        throw new InvalidOperationException();
    }

    public App(OverrideAssetsManager overrideAssets)
    {
        _overrideAssets = overrideAssets;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        LoadBaseAssets();
        IconsLoader.Load(this);

        _overrideAssets.AssetsChanged += OnAssetsChanged;
    }

    private void LoadBaseAssets()
    {
        foreach (var (name, (path, type)) in AssetDefs)
        {
            using var dataStream = AssetLoader.Open(new Uri($"avares://SS14.Launcher/Assets/{path}"));

            var asset = LoadAsset(type, dataStream);

            _baseAssets.Add(name, asset);
            Resources.Add(name, asset);
        }
    }

    private void OnAssetsChanged(OverrideAssetsChanged obj)
    {
        foreach (var (name, data) in obj.Files)
        {
            if (!AssetDefs.TryGetValue(name, out var def))
            {
                Log.Warning("Unable to find asset def for asset: '{AssetName}'", name);
                continue;
            }

            var ms = new MemoryStream(data, writable: false);
            var asset = LoadAsset(def.Type, ms);

            Resources[name] = asset;
        }

        // Clear assets not given to base data.
        foreach (var (name, asset) in _baseAssets)
        {
            if (!obj.Files.ContainsKey(name))
                Resources[name] = asset;
        }
    }

    private static object LoadAsset(AssetType type, Stream data)
    {
        return type switch
        {
            AssetType.Bitmap => new Bitmap(data),
            AssetType.WindowIcon => new WindowIcon(data),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private sealed record AssetDef(string DefaultPath, AssetType Type);

    private enum AssetType
    {
        Bitmap,
        WindowIcon
    }

    // Called when Avalonia init is done
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Startup += OnStartup;
            desktop.Exit += OnExit;
        }
    }

    private void OnStartup(object? s, ControlledApplicationLifetimeStartupEventArgs e)
    {
        var loc = Locator.Current.GetRequiredService<LocalizationManager>();
        var msgr = Locator.Current.GetRequiredService<LauncherMessaging>();
        var contentManager = Locator.Current.GetRequiredService<ContentManager>();
        var overrideAssets = Locator.Current.GetRequiredService<OverrideAssetsManager>();
        var launcherInfo = Locator.Current.GetRequiredService<LauncherInfoManager>();

        loc.Initialize();
        launcherInfo.Initialize();
        contentManager.Initialize();
        overrideAssets.Initialize();

        var viewModel = new MainWindowViewModel();
        var window = new MainWindow
        {
            DataContext = viewModel
        };
        viewModel.OnWindowInitialized();

        loc.LanguageSwitched += () =>
        {
            window.ReloadContent();

            // Reloading content isn't a smooth process anyway, so let's do some housekeeping while we're at it.
            GC.Collect();
        };

        var lc = new LauncherCommands(viewModel);
        lc.RunCommandTask();
        Locator.CurrentMutable.RegisterConstant(lc);
        msgr.StartServerTask(lc);

        window.Show();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var msgr = Locator.Current.GetRequiredService<LauncherMessaging>();
        msgr.StopAndWait();
    }
}
