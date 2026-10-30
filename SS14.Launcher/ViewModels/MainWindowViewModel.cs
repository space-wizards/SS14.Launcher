using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
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
            new DevelopmentTabViewModel(this),
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

    // Errors that happened while another overlay was up, shown one after the other as each is dismissed.
    private const int MaxPendingOverlays = 5;
    private readonly Queue<AuthErrorsOverlayViewModel> _pendingOverlays = new();

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
        var reachable = true;

        if (IsRunningFromTranslocatedLocation())
        {
            Log.Warning("Running from a translocated location ({ErrorCode})", ErrorCodes.GenTranslocated);
            ShowErrorOverlay("error-overlay-title-translocated", "error-overlay-desc-translocated", ErrorCodes.GenTranslocated);
        }

        try
        {
            ErrorCode? offlineCode = null;

            // The OS can be wrong about having no network (some virtual or tunnel adapters aren't counted),
            // so before believing it, check that the auth server's name really can't be found.
            if (!NetworkInterface.GetIsNetworkAvailable() && !await CanFindAuthServerAsync())
            {
                // Nothing we require is reachable, so don't make the user wait for every request to time out.
                offlineCode = ErrorCodes.NetNoConnection;
            }
            else
            {
                BusyTask = _loc.GetString("main-window-busy-checking-update");
                await CheckLauncherUpdate();

                // Other failures, such as the server answering with an error, are no reason to treat the user as
                // offline. We carry on like before and assume this launcher is up to date.
                if (_infoManager.FailureCode is { } failure && await IsOfflineFailureAsync(failure))
                    offlineCode = failure;
            }

            if (offlineCode is { } code)
            {
                reachable = false;
                ShowOfflineError(code);
            }
            else
            {
                BusyTask = _loc.GetString("main-window-busy-checking-login-status");
            }

            // Logins are always set up, so the token refresh timer runs and the saved accounts stay.
            // Only the check against the auth server right now is skipped if we know it would just time out.
            await CheckAccounts(reachable);
        }
        catch (Exception e)
        {
            // Whatever goes wrong here, we can't keep the spinny thing going for all eternity.
            reachable = false;
            var code = ErrorClassifier.ClassifyPrecisely(e) ?? ErrorCodes.GenUnexpected;
            Log.Error(e, "Startup checks failed ({ErrorCode})", code);
            ShowErrorOverlay("error-overlay-title-unexpected", "error-overlay-desc-unexpected", code);
        }
        finally
        {
            BusyTask = null;
        }

        // Selecting the saved account would only try the auth server again, and show a second error.
        if (reachable && _cfg.SelectedLoginId is { } g && _loginMgr.Logins.TryLookup(g, out var login))
        {
            TrySwitchToAccount(login);
        }

        // We should now start reacting to commands.
    }

    private static async Task<bool> IsOfflineFailureAsync(ErrorCode code)
    {
        if (code == ErrorCodes.NetNoConnection || code == ErrorCodes.NetDnsFailed)
            return true;

        // Only the launcher info server was slow or refused us, which says nothing about the auth server.
        // This could happen with Russian censorship where they throttle downloads.
        // The rest of the launcher is likely to be unstable or unusable, but it isn't a reason to fail.
        if (code == ErrorCodes.NetConnectFailed || code == ErrorCodes.NetTimeout)
            return !await CanFindAuthServerAsync();

        return false;
    }

    private static async Task<bool> CanFindAuthServerAsync()
    {
        try
        {
            var host = new Uri(ConfigConstants.AuthUrl.Urls[0]).Host;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await Dns.GetHostAddressesAsync(host, timeout.Token);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
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
        // The message already tells the user to check their connection, so skip the hint that says the same.
        ShowErrorOverlay("error-overlay-title-offline", "error-overlay-desc-offline", code, showHint: false);
    }

    private async Task CheckAccounts(bool refreshNow)
    {
        // Check if accounts are still valid and refresh their tokens if necessary.
        await _loginMgr.Initialize(refreshNow);
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
            Log.Warning(e, "AuthApiException while trying to refresh account {login} ({ErrorCode})", account.LoginInfo, e.Code);
            PushOverlay(new AuthErrorsOverlayViewModel(this, _loc.GetString("main-window-error-connecting-auth-server"),
                new[]
                {
                    e.InnerException?.Message ?? _loc.GetString("main-window-error-unknown")
                },
                e.Code));
        }
        finally
        {
            BusyTask = null;
        }
    }

    /// <summary>
    /// Shows an error overlay with the given localized text and error code.
    /// </summary>
    public void ShowErrorOverlay(string titleKey, string descriptionKey, ErrorCode code, bool showHint = true)
    {
        PushOverlay(new AuthErrorsOverlayViewModel(
            this,
            _loc.GetString(titleKey),
            new[] { _loc.GetString(descriptionKey) },
            code,
            showHint: showHint));
    }

    /// <summary>
    /// Shows the overlay now, or once the ones before it have been dismissed. Same error twice in a row is shown once.
    /// </summary>
    private void PushOverlay(AuthErrorsOverlayViewModel overlay)
    {
        if (OverlayViewModel == null)
        {
            OverlayViewModel = overlay;
            return;
        }

        if (IsSameError(OverlayViewModel, overlay) || _pendingOverlays.Any(p => IsSameError(p, overlay)))
            return;

        // Errors that keep coming are the crash handler's problem, don't pile up a stack of them.
        if (_pendingOverlays.Count < MaxPendingOverlays)
            _pendingOverlays.Enqueue(overlay);
    }

    private static bool IsSameError(ViewModelBase? shown, AuthErrorsOverlayViewModel other)
    {
        return shown is AuthErrorsOverlayViewModel o && o.Code == other.Code && o.Title == other.Title;
    }

    /// <summary>
    /// Shows an error overlay for a failed connection. Unlike <see cref="ShowErrorOverlay"/> it takes text that
    /// is already put together, and it replaces any overlay that is up, since the user is waiting for this.
    /// </summary>
    public void ShowFailureOverlay(string title, string message, ErrorCode code, string codeText)
    {
        // An empty message means the title says it all, so don't leave a blank line.
        var messages = message.Length == 0 ? Array.Empty<string>() : new[] { message };

        OverlayViewModel = new AuthErrorsOverlayViewModel(this, title, messages, code, codeText);
    }

    public void OverlayOk()
    {
        OverlayViewModel = _pendingOverlays.Count > 0 ? _pendingOverlays.Dequeue() : null;
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
