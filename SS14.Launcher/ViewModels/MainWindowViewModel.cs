using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using Splat;
using SS14.Launcher.Api;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels.Login;
using SS14.Launcher.ViewModels.MainWindowTabs;
using SS14.Launcher.Views;

namespace SS14.Launcher.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IErrorOverlayOwner
{
    private readonly DataManager _cfg;
    private readonly LoginManager _loginMgr;
    private readonly HttpClient _http;
    private readonly LauncherInfoManager _infoManager;
    private readonly LocalizationManager _loc;

    private int _selectedIndex;

    public DataManager Cfg => _cfg;
    [Reactive] public bool OutOfDate { get; private set; }

    public HomePageViewModel HomeTab { get; }
    public ServerListTabViewModel ServersTab { get; }
    public NewsTabViewModel NewsTab { get; }
    public OptionsTabViewModel OptionsTab { get; }

    public MainWindowViewModel()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _loginMgr = Locator.Current.GetRequiredService<LoginManager>();
        _http = Locator.Current.GetRequiredService<HttpClient>();
        _infoManager = Locator.Current.GetRequiredService<LauncherInfoManager>();
        _loc = LocalizationManager.Instance;

        ServersTab = new ServerListTabViewModel(this);
        NewsTab = new NewsTabViewModel();
        HomeTab = new HomePageViewModel(this);
        OptionsTab = new OptionsTabViewModel();

        var tabs = new List<MainWindowTabViewModel>();
        tabs.Add(HomeTab);
        tabs.Add(ServersTab);
        tabs.Add(NewsTab);
        tabs.Add(OptionsTab);
#if DEVELOPMENT
        tabs.Add(new DevelopmentTabViewModel());
#endif
        Tabs = tabs;

        AccountDropDown = new AccountDropDownViewModel(this);
        LoginViewModel = new MainWindowLoginViewModel();

        this.WhenAnyValue(x => x._loginMgr.ActiveAccount)
            .Subscribe(s =>
            {
                this.RaisePropertyChanged(nameof(Username));
                this.RaisePropertyChanged(nameof(LoggedIn));
            });

        _cfg.Logins.Connect()
            .Subscribe(_ => { this.RaisePropertyChanged(nameof(AccountDropDownVisible)); });

        // If we leave the login view model (by an account getting selected)
        // we reset it to login state
        this.WhenAnyValue(x => x.LoggedIn)
            .DistinctUntilChanged() // Only when change.
            .Subscribe(x =>
            {
                if (x)
                {
                    // "Switch" to main window.
                    RunSelectedOnTab();
                }
                else
                {
                    LoginViewModel.SwitchToLogin();
                }
            });
    }

    public MainWindow? Control { get; set; }

    public IReadOnlyList<MainWindowTabViewModel> Tabs { get; }

    public bool LoggedIn => _loginMgr.ActiveAccount != null;
    private string? Username => _loginMgr.ActiveAccount?.Username;
    public bool AccountDropDownVisible => _loginMgr.Logins.Count != 0;

    public AccountDropDownViewModel AccountDropDown { get; }

    public MainWindowLoginViewModel LoginViewModel { get; }

    [Reactive] public ConnectingViewModel? ConnectingVM { get; set; }

    [Reactive] public string? BusyTask { get; private set; }
    [Reactive] public ViewModelBase? OverlayViewModel { get; private set; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var previous = Tabs[_selectedIndex];
            previous.IsSelected = false;

            this.RaiseAndSetIfChanged(ref _selectedIndex, value);

            RunSelectedOnTab();
        }
    }

    private void RunSelectedOnTab()
    {
        var tab = Tabs[_selectedIndex];
        tab.IsSelected = true;
        tab.Selected();
    }

    public ICVarEntry<bool> HasDismissedEarlyAccessWarning => Cfg.GetCVarEntry(CVars.HasDismissedEarlyAccessWarning);
    public bool ShouldShowIntelDegradationWarning => IsVulnerableToIntelDegradation(_cfg);
    public bool ShouldShowRosettaWarning => IsAppleSiliconInRosetta(_cfg);

    public string Version => $"v{LauncherVersion.Version}";

    public async void OnWindowInitialized()
    {
        BusyTask = _loc.GetString("main-window-busy-checking-update");
        await CheckLauncherUpdate();
        BusyTask = _loc.GetString("main-window-busy-checking-login-status");
        await CheckAccounts();
        BusyTask = null;

        if (_cfg.SelectedLoginId is { } g && _loginMgr.Logins.TryLookup(g, out var login))
        {
            TrySwitchToAccount(login);
        }

        // We should now start reacting to commands.
    }

    private async Task CheckAccounts()
    {
        // Check if accounts are still valid and refresh their tokens if necessary.
        await _loginMgr.Initialize();
    }

    public void OnDiscordButtonPressed()
    {
        Helpers.OpenUri(new Uri(ConfigConstants.DiscordUrl));
    }

    public void OnWebsiteButtonPressed()
    {
        Helpers.OpenUri(new Uri(ConfigConstants.WebsiteUrl));
    }

    private async Task CheckLauncherUpdate()
    {
        // await Task.Delay(1000);
        if (!ConfigConstants.DoVersionCheck)
        {
            return;
        }

        await _infoManager.LoadTask;
        if (_infoManager.Model == null)
        {
            // Error while loading.
            Log.Warning("Unable to check for launcher update due to error, assuming up-to-date.");
            OutOfDate = false;
            return;
        }

        OutOfDate = Array.IndexOf(_infoManager.Model.AllowedVersions, ConfigConstants.CurrentLauncherVersion) == -1;
        Log.Debug("Launcher out of date? {Value}", OutOfDate);
    }

    public void ExitPressed()
    {
        Control?.Close();
    }

    public void DownloadPressed()
    {
        Helpers.OpenUri(new Uri(ConfigConstants.DownloadUrl));
    }

    public void DismissEarlyAccessPressed()
    {
        Cfg.SetCVar(CVars.HasDismissedEarlyAccessWarning, true);
        Cfg.CommitConfig();
    }

    public void DismissIntelDegradationPressed()
    {
        Cfg.SetCVar(CVars.HasDismissedIntelDegradation, true);
        Cfg.CommitConfig();
        this.RaisePropertyChanged(nameof(ShouldShowIntelDegradationWarning));
    }

    public void DismissAppleSiliconRosettaPressed()
    {
        Cfg.SetCVar(CVars.HasDismissedRosettaWarning, true);
        Cfg.CommitConfig();
        this.RaisePropertyChanged(nameof(ShouldShowRosettaWarning));
    }

    public void SelectTabServers()
    {
        SelectedIndex = Tabs.IndexOf(ServersTab);
    }

    public void TrySwitchToAccount(LoggedInAccount account)
    {
        switch (account.Status)
        {
            case AccountLoginStatus.Unsure:
                TrySelectUnsureAccount(account);
                break;

            case AccountLoginStatus.Available:
                _loginMgr.ActiveAccount = account;
                break;

            case AccountLoginStatus.Expired:
                _loginMgr.ActiveAccount = null;
                LoginViewModel.SwitchToExpiredLogin(account);
                break;
        }
    }

    private async void TrySelectUnsureAccount(LoggedInAccount account)
    {
        BusyTask = _loc.GetString("main-window-busy-checking-account-status");
        try
        {
            await _loginMgr.UpdateSingleAccountStatus(account);

            // Can't be unsure, that'd have thrown.
            Debug.Assert(account.Status != AccountLoginStatus.Unsure);
            TrySwitchToAccount(account);
        }
        catch (AuthApiException e)
        {
            Log.Warning(e, "AuthApiException while trying to refresh account {login}", account.LoginInfo);
            OverlayViewModel = new AuthErrorsOverlayViewModel(this, _loc.GetString("main-window-error-connecting-auth-server"),
                new[]
                {
                    e.InnerException?.Message ?? _loc.GetString("main-window-error-unknown")
                });
        }
        finally
        {
            BusyTask = null;
        }
    }

    public void OverlayOk()
    {
        OverlayViewModel = null;
    }

    public bool IsContentBundleDropValid(IStorageFile file)
    {
        // Can only load content bundles if logged in, in some capacity.
        if (!LoggedIn)
            return false;

        // Disallow if currently connecting to a server.
        if (ConnectingVM != null)
            return false;

        return Path.GetExtension(file.Name) == ".zip";
    }

    public void Dropped(IStorageFile file)
    {
        // Trust view validated this.
        Debug.Assert(IsContentBundleDropValid(file));

        ConnectingViewModel.StartContentBundle(this, file);
    }

    private static bool IsVulnerableToIntelDegradation(DataManager cfg)
    {
        var processor = LauncherDiagnostics.GetProcessorModel();

        // No Intel processor, or already dismissed the warning.
        if (!processor.Contains("Intel") || cfg.GetCVar(CVars.HasDismissedIntelDegradation))
            return false;

        // Get the i#-#### from the processor string.
        var match = Regex.Match(processor, @"i\d+-\d+(?:[A-Z]+)?(?=\s|$)");
        if (!match.Success)
            return false;

        var affectedGenerations = new[] { "i3-13", "i5-13", "i7-13", "i9-13", "i3-14", "i5-14", "i7-14", "i9-14" };
        var excludedSuffixes = new[] { "HX", "H", "P", "U" };

        return affectedGenerations.Any(match.Value.Contains) && !excludedSuffixes.Any(match.Value.EndsWith);
    }

    private static bool IsAppleSiliconInRosetta(DataManager cfg)
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        var processor = LauncherDiagnostics.GetProcessorModel();

        return processor.Contains("VirtualApple") && !cfg.GetCVar(CVars.HasDismissedRosettaWarning);
    }
}
