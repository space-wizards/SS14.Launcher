using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DynamicData;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
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

    private IDisposable? _authOverrideCountdownTimer;

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

        // Things without a UI of their own (saving settings, the options tab) report errors through the messenger.
        WeakReferenceMessenger.Default.Register<MainWindowViewModel, ErrorOccurred>(this, static (vm, message) =>
            Dispatcher.UIThread.Post(() => vm.ShowErrorOverlay(message.TitleKey, message.DescriptionKey, message.Code)));

        _loginMgr.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_loginMgr.ActiveAccount))
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(LoggedIn)));
        };

        _cfg.Logins.Connect()
            .Subscribe(_ => OnPropertyChanged(new PropertyChangedEventArgs(nameof(AccountDropDownVisible))));
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

    public ICVarEntry<bool> HasDismissedEarlyAccessWarning => Cfg.GetCVarEntry(CVars.HasDismissedEarlyAccessWarning);
    public bool ShouldShowIntelDegradationWarning => IsVulnerableToIntelDegradation(_cfg);
    public bool ShouldShowRosettaWarning => IsAppleSiliconInRosetta(_cfg);
    [ObservableProperty] private bool _shouldShowAuthOverrideWarning;
    [ObservableProperty] private int _authOverrideCountdown = 5;
    [ObservableProperty] private bool _isAuthOverrideButtonEnabled;

    public string Version => $"v{LauncherVersion.Version}";

    public async void OnWindowInitialized()
    {
        var online = false;

        if (IsRunningFromTranslocatedLocation())
        {
            Log.Warning("Running from a translocated location ({ErrorCode})", ErrorCodes.GenTranslocated);
            ShowErrorOverlay("error-overlay-title-translocated", "error-overlay-desc-translocated", ErrorCodes.GenTranslocated);
        }

        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                // Nothing we require is reachable, so don't make the user wait for every request to time out.
                ShowOfflineError(ErrorCodes.NetNoConnection);
            }
            else
            {
                BusyTask = _loc.GetString("main-window-busy-checking-update");
                await CheckLauncherUpdate();

                if (_infoManager.FailureCode is { } failure)
                {
                    // Without launcher info we can't tell whether this launcher is even allowed to run,
                    // and the login checks would only time out the same way.
                    ShowOfflineError(failure);
                }
                else
                {
                    online = true;
                    BusyTask = _loc.GetString("main-window-busy-checking-login-status");
                    await CheckAccounts();
                }
            }
        }
        catch (Exception e)
        {
            // Whatever goes wrong here, we can't keep the spinny thing going for all eternity.
            online = false;
            var code = ErrorClassifier.ClassifyPrecisely(e) ?? ErrorCodes.GenUnexpected;
            Log.Error(e, "Startup checks failed ({ErrorCode})", code);
            ShowErrorOverlay("error-overlay-title-unexpected", "error-overlay-desc-unexpected", code);
        }
        finally
        {
            BusyTask = null;
        }

        if (online && _cfg.SelectedLoginId is { } g && _loginMgr.Logins.TryLookup(g, out var login))
        {
            TrySwitchToAccount(login);
        }

        // We should now start reacting to commands.
    }

    // macOS runs a quarantined app from a random read-only copy ("App Translocation", according to the docs) until it is moved somewhere.
    private static bool IsRunningFromTranslocatedLocation()
    {
        return OperatingSystem.IsMacOS()
               && LauncherPaths.DirLauncherInstall.Contains("/AppTranslocation/", StringComparison.Ordinal);
    }

    private void ShowOfflineError(ErrorCode code)
    {
        Log.Warning("Cannot reach the launcher's servers ({ErrorCode})", code);
        ShowErrorOverlay("error-overlay-title-offline", "error-overlay-desc-offline", code);
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
        Cfg.SetCVar(CVars.HasDismissedEarlyAccessWarning, true);
        Cfg.CommitConfig();
    }

    public void DismissIntelDegradationPressed()
    {
        Cfg.SetCVar(CVars.HasDismissedIntelDegradation, true);
        Cfg.CommitConfig();
        OnPropertyChanged(nameof(ShouldShowIntelDegradationWarning));
    }

    public void DismissAppleSiliconRosettaPressed()
    {
        Cfg.SetCVar(CVars.HasDismissedRosettaWarning, true);
        Cfg.CommitConfig();
        OnPropertyChanged(nameof(ShouldShowRosettaWarning));
    }

    public void DismissAuthOverridePressed()
    {
        _authOverrideCountdownTimer?.Dispose();
        _authOverrideCountdownTimer = null;
        ShouldShowAuthOverrideWarning = false;
    }

    public void StartAuthOverrideCountdown()
    {
        AuthOverrideCountdown = 5;
        IsAuthOverrideButtonEnabled = false;
        _authOverrideCountdownTimer?.Dispose();

        _authOverrideCountdownTimer = DispatcherTimer.Run(() =>
        {
            AuthOverrideCountdown--;
            if (AuthOverrideCountdown <= 0)
            {
                IsAuthOverrideButtonEnabled = true;
                _authOverrideCountdownTimer?.Dispose();
                _authOverrideCountdownTimer = null;
                return false;
            }
            return true;
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
            Log.Warning("Account refresh failed ({ErrorCode})", e.Code);
            OverlayViewModel = new AuthErrorsOverlayViewModel(this, _loc.GetString("main-window-error-connecting-auth-server"),
                new[]
                {
                    e.InnerException?.Message ?? _loc.GetString("main-window-error-unknown")
                },
                e.Code);
        }
        finally
        {
            BusyTask = null;
        }
    }

    /// <summary>
    /// Shows an error overlay with the given localized text and error code.
    /// </summary>
    public void ShowErrorOverlay(string titleKey, string descriptionKey, ErrorCode code)
    {
        if (OverlayViewModel != null)
            return;

        OverlayViewModel = new AuthErrorsOverlayViewModel(
            this,
            _loc.GetString(titleKey),
            new[] { _loc.GetString(descriptionKey) },
            code);
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
