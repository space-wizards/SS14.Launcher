using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DynamicData;
using Microsoft.Toolkit.Mvvm.ComponentModel;
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

public sealed partial class MainWindowViewModel : ViewModelBase, IErrorOverlayOwner
{
    private readonly DataManager _cfg;
    private readonly LoginManager _loginMgr;
    private readonly LauncherInfoManager _infoManager;
    private readonly LocalizationManager _loc;

    private int _selectedIndex;

    public DataManager Cfg => _cfg;
    [ObservableProperty] private bool _outOfDate;

    private IDisposable? _warningCountdownTimer;

    public HomePageViewModel HomeTab { get; }
    public ServerListTabViewModel ServersTab { get; }
    public NewsTabViewModel NewsTab { get; }
    public OptionsTabViewModel OptionsTab { get; }

    public MainWindowViewModel()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _loginMgr = Locator.Current.GetRequiredService<LoginManager>();
        _infoManager = Locator.Current.GetRequiredService<LauncherInfoManager>();
        _loc = LocalizationManager.Instance;

        ServersTab = new ServerListTabViewModel(this);
        NewsTab = new NewsTabViewModel();
        HomeTab = new HomePageViewModel(this);
        OptionsTab = new OptionsTabViewModel();

        Tabs = new List<MainWindowTabViewModel>
        {
            HomeTab,
            ServersTab,
            NewsTab,
            OptionsTab,
#if DEVELOPMENT
            new DevelopmentTabViewModel(),
#endif
        };

        AccountDropDown = new AccountDropDownViewModel(this);
        LoginViewModel = new MainWindowLoginViewModel();

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LoggedIn) && LoggedIn)
                RunSelectedOnTab();
        };

        _loginMgr.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_loginMgr.ActiveAccount))
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(LoggedIn)));
        };

        _cfg.Logins.Connect()
            .Subscribe(_ => OnPropertyChanged(new PropertyChangedEventArgs(nameof(AccountDropDownVisible))));

        // Start the countdown if the first warning to show needs one.
        WarningsChanged();
    }

    public MainWindow? Control { get; set; }

    public IReadOnlyList<MainWindowTabViewModel> Tabs { get; }

    public bool LoggedIn => _loginMgr.ActiveAccount != null;
    public bool AccountDropDownVisible => _loginMgr.Logins.Count != 0;

    public AccountDropDownViewModel AccountDropDown { get; }

    public MainWindowLoginViewModel LoginViewModel { get; }

    [ObservableProperty] private ConnectingViewModel? _connectingVM;

    [ObservableProperty] private string? _busyTask;
    [ObservableProperty] private ViewModelBase? _overlayViewModel;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var previous = Tabs[_selectedIndex];
            previous.IsSelected = false;

            if (!EqualityComparer<int>.Default.Equals(_selectedIndex, value))
            {
                OnPropertyChanging();
                _selectedIndex = value;
                OnPropertyChanged();
            }

            RunSelectedOnTab();
        }
    }

    private void RunSelectedOnTab()
    {
        var tab = Tabs[_selectedIndex];
        tab.IsSelected = true;
        tab.Selected();
    }

    // Warning overlays are shown one at a time, highest priority first.
    private enum ActiveWarning
    {
        None,
        AuthOverride,
        EarlyAccess,
        Epilepsy,
        IntelDegradation,
        Rosetta
    }

    private ActiveWarning CurrentWarning
    {
        get
        {
            if (ShouldShowAuthOverrideWarning)
                return ActiveWarning.AuthOverride;
            if (!IsDismissed(ActiveWarning.EarlyAccess))
                return ActiveWarning.EarlyAccess;
            if (!IsDismissed(ActiveWarning.Epilepsy))
                return ActiveWarning.Epilepsy;
            if (!IsDismissed(ActiveWarning.IntelDegradation) && IsVulnerableToIntelDegradation())
                return ActiveWarning.IntelDegradation;
            if (!IsDismissed(ActiveWarning.Rosetta) && IsAppleSiliconInRosetta())
                return ActiveWarning.Rosetta;

            return ActiveWarning.None;
        }
    }

    private bool IsDismissed(ActiveWarning warning)
    {
        return Cfg.GetCVar(CVars.DismissedWarnings).Split(',').Contains(warning.ToString());
    }

    private void Dismiss(ActiveWarning warning)
    {
        var dismissed = Cfg.GetCVar(CVars.DismissedWarnings).Split(',', StringSplitOptions.RemoveEmptyEntries);
        Cfg.SetCVar(CVars.DismissedWarnings, string.Join(',', dismissed.Append(warning.ToString()).Distinct()));
        Cfg.CommitConfig();
        WarningsChanged();
    }

    public bool ShowEarlyAccessWarning => CurrentWarning == ActiveWarning.EarlyAccess;
    public bool ShowEpilepsyWarning => CurrentWarning == ActiveWarning.Epilepsy;
    public bool ShowIntelDegradationWarning => CurrentWarning == ActiveWarning.IntelDegradation;
    public bool ShowRosettaWarning => CurrentWarning == ActiveWarning.Rosetta;
    public bool ShowAuthOverrideWarning => CurrentWarning == ActiveWarning.AuthOverride;

    private ActiveWarning _lastWarning;
    private int _warningCountdown;

    /// <summary>
    /// Seconds left before the current warning can be acknowledged. 0 if it can be acknowledged right away.
    /// </summary>
    public int WarningCountdown
    {
        get => _warningCountdown;
        private set
        {
            if (!SetProperty(ref _warningCountdown, value))
                return;

            OnPropertyChanged(nameof(IsWarningButtonEnabled));
            OnPropertyChanged(nameof(AuthOverrideButtonText));
            OnPropertyChanged(nameof(IntelDegradationButtonText));
        }
    }

    public bool IsWarningButtonEnabled => WarningCountdown <= 0;

    public string AuthOverrideButtonText => AcceptButtonText("main-window-auth-override-acknowledge");
    public string IntelDegradationButtonText => AcceptButtonText("main-window-intel-degrade-accept");

    private string AcceptButtonText(string key)
    {
        var text = _loc.GetString(key);
        if (WarningCountdown <= 0)
            return text;

        return _loc.GetString("main-window-warning-accept-countdown",
            ("text", text), ("seconds", WarningCountdown));
    }

    private void WarningsChanged()
    {
        var current = CurrentWarning;
        if (current != _lastWarning)
        {
            _lastWarning = current;
            RestartWarningCountdown(current);
        }

        OnPropertyChanged(nameof(ShowEarlyAccessWarning));
        OnPropertyChanged(nameof(ShowEpilepsyWarning));
        OnPropertyChanged(nameof(ShowIntelDegradationWarning));
        OnPropertyChanged(nameof(ShowRosettaWarning));
        OnPropertyChanged(nameof(ShowAuthOverrideWarning));
    }

    private bool _shouldShowAuthOverrideWarning;

    public bool ShouldShowAuthOverrideWarning
    {
        get => _shouldShowAuthOverrideWarning;
        set
        {
            if (SetProperty(ref _shouldShowAuthOverrideWarning, value))
                WarningsChanged();
        }
    }

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

        var currentVersion = LauncherVersion.Version;
        if (currentVersion == null)
        {
            Log.Error("Unable to determine launcher version.");
            OutOfDate = true;
            return;
        }

        OutOfDate = !LauncherInfoManager.IsVersionAllowed(_infoManager.Model, currentVersion);
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
        Dismiss(ActiveWarning.EarlyAccess);
    }

    public void DismissEpilepsyWarningPressed()
    {
        Dismiss(ActiveWarning.Epilepsy);
    }

    public void DismissIntelDegradationPressed()
    {
        Dismiss(ActiveWarning.IntelDegradation);
    }

    public void DismissAppleSiliconRosettaPressed()
    {
        Dismiss(ActiveWarning.Rosetta);
    }

    public void DismissAuthOverridePressed()
    {
        ShouldShowAuthOverrideWarning = false;
    }

    private void RestartWarningCountdown(ActiveWarning warning)
    {
        _warningCountdownTimer?.Dispose();
        _warningCountdownTimer = null;

        WarningCountdown = warning is ActiveWarning.AuthOverride or ActiveWarning.IntelDegradation ? 5 : 0;
        if (WarningCountdown <= 0)
            return;

        _warningCountdownTimer = DispatcherTimer.Run(() =>
        {
            WarningCountdown--;
            return WarningCountdown > 0;
        }, TimeSpan.FromSeconds(1), DispatcherPriority.Normal);
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

    private static bool IsVulnerableToIntelDegradation()
    {
        var processor = LauncherDiagnostics.GetProcessorModel();

        if (!processor.Contains("Intel"))
            return false;

        // Get the i#-#### from the processor string.
        var match = Regex.Match(processor, @"i\d+-\d+(?:[A-Z]+)?(?=\s|$)");
        if (!match.Success)
            return false;

        var affectedGenerations = new[] { "i3-13", "i5-13", "i7-13", "i9-13", "i3-14", "i5-14", "i7-14", "i9-14" };
        var excludedSuffixes = new[] { "HX", "H", "P", "U" };

        return affectedGenerations.Any(match.Value.Contains) && !excludedSuffixes.Any(match.Value.EndsWith);
    }

    private static bool IsAppleSiliconInRosetta()
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        var processor = LauncherDiagnostics.GetProcessorModel();

        return processor.Contains("VirtualApple");
    }
}
