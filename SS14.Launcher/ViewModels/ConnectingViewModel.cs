using System;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Platform.Storage;
using ReactiveUI;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels;

public class ConnectingViewModel : ViewModelBase
{
    private readonly Connector _connector;
    private readonly Updater _updater;
    private readonly MainWindowViewModel _windowVm;
    private readonly ConnectionType _connectionType;
    private readonly LocalizationManager _loc;

    private readonly CancellationTokenSource _cancelSource = new CancellationTokenSource();

    private string? _reasonSuffix;

    private Connector.ConnectionStatus _connectorStatus;
    private Updater.UpdateStatus _updaterStatus;
    private (long downloaded, long total, Updater.ProgressUnit unit)? _updaterProgress;
    private long? _updaterSpeed;

    public bool IsErrored => _connectorStatus == Connector.ConnectionStatus.ConnectionFailed ||
                             _connectorStatus == Connector.ConnectionStatus.UpdateError ||
                             _connectorStatus == Connector.ConnectionStatus.NotAContentBundle ||
                             _connectorStatus == Connector.ConnectionStatus.ClientExited &&
                             _connector.ClientExitedBadly;

    public static event Action? StartedConnecting;

    public ConnectingViewModel(Connector connector, MainWindowViewModel windowVm, string? givenReason, ConnectionType connectionType)
    {
        _updater = Locator.Current.GetRequiredService<Updater>();
        _loc = LocalizationManager.Instance;
        _connector = connector;
        _windowVm = windowVm;
        _connectionType = connectionType;
        _reasonSuffix = (givenReason != null) ? ("\n" + givenReason) : "";

        this.WhenAnyValue(x => x._updater.Progress)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(progress =>
            {
                _updaterProgress = progress;

                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(ProgressIndeterminate));
                this.RaisePropertyChanged(nameof(ProgressText));
            });

        this.WhenAnyValue(x => x._updater.Speed)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(speed =>
            {
                _updaterSpeed = speed;

                this.RaisePropertyChanged(nameof(SpeedText));
                this.RaisePropertyChanged(nameof(SpeedIndeterminate));
            });

        this.WhenAnyValue(x => x._updater.Status)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(status =>
            {
                _updaterStatus = status;
                this.RaisePropertyChanged(nameof(StatusText));
            });

        this.WhenAnyValue(x => x._connector.Status)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(val =>
            {
                _connectorStatus = val;

                this.RaisePropertyChanged(nameof(ProgressIndeterminate));
                this.RaisePropertyChanged(nameof(StatusText));
                this.RaisePropertyChanged(nameof(ProgressBarVisible));
                this.RaisePropertyChanged(nameof(IsErrored));
                this.RaisePropertyChanged(nameof(IsAskingPrivacyPolicy));

                if (val == Connector.ConnectionStatus.ClientRunning
                    || val == Connector.ConnectionStatus.Cancelled
                    || val == Connector.ConnectionStatus.ClientExited && !_connector.ClientExitedBadly)
                {
                    CloseOverlay();
                }
            });

        this.WhenAnyValue(x => x._connector.PrivacyPolicyDifferentVersion)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(PrivacyPolicyText));
            });

        this.WhenAnyValue(x => x._connector.ClientExitedBadly)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(StatusText));
                this.RaisePropertyChanged(nameof(IsErrored));
            });
    }

    public float Progress
    {
        get
        {
            if (_updaterProgress == null)
            {
                return 0;
            }

            var (downloaded, total, _) = _updaterProgress.Value;

            return downloaded / (float)total;
        }
    }

    public string ProgressText
    {
        get
        {
            if (_updaterProgress == null)
            {
                return "";
            }

            var (downloaded, total, unit) = _updaterProgress.Value;

            return unit switch
            {
                Updater.ProgressUnit.Bytes => $"{Helpers.FormatBytes(downloaded)} / {Helpers.FormatBytes(total)}",
                _ => $"{downloaded} / {total}"
            };
        }
    }

    public bool ProgressIndeterminate => _connectorStatus != Connector.ConnectionStatus.Updating
                                         || _updaterProgress == null;

    public bool ProgressBarVisible => _connectorStatus != Connector.ConnectionStatus.ClientExited &&
                                      _connectorStatus != Connector.ConnectionStatus.ClientRunning &&
                                      _connectorStatus != Connector.ConnectionStatus.ConnectionFailed &&
                                      _connectorStatus != Connector.ConnectionStatus.UpdateError &&
                                      _connectorStatus != Connector.ConnectionStatus.NotAContentBundle;

    public bool SpeedIndeterminate => _connectorStatus != Connector.ConnectionStatus.Updating || _updaterSpeed == null;

    public string SpeedText
    {
        get
        {
            if (_updaterSpeed is not { } speed)
                return "";

            return $"{Helpers.FormatBytes(speed)}/s";
        }
    }

    public string StatusText =>
        _connectorStatus switch
        {
            Connector.ConnectionStatus.None => _loc.GetString("connecting-status-none") + _reasonSuffix,
            Connector.ConnectionStatus.UpdateError => FormatUpdateError(),
            Connector.ConnectionStatus.Updating => _loc.GetString("connecting-status-updating", ("status", _loc.GetString(_updaterStatus switch
            {
                Updater.UpdateStatus.CheckingClientUpdate => "connecting-update-status-checking-client-update",
                Updater.UpdateStatus.DownloadingEngineVersion => "connecting-update-status-downloading-engine",
                Updater.UpdateStatus.DownloadingClientUpdate => "connecting-update-status-downloading-content",
                Updater.UpdateStatus.FetchingClientManifest => "connecting-update-status-fetching-manifest",
                Updater.UpdateStatus.Verifying => "connecting-update-status-verifying",
                Updater.UpdateStatus.CullingEngine => "connecting-update-status-culling-engine",
                Updater.UpdateStatus.CullingContent => "connecting-update-status-culling-content",
                Updater.UpdateStatus.Ready => "connecting-update-status-ready",
                Updater.UpdateStatus.CheckingEngineModules => "connecting-update-status-checking-engine-modules",
                Updater.UpdateStatus.DownloadingEngineModules => "connecting-update-status-downloading-engine-modules",
                Updater.UpdateStatus.CommittingDownload => "connecting-update-status-committing-download",
                Updater.UpdateStatus.LoadingIntoDb => "connecting-update-status-loading-into-db",
                Updater.UpdateStatus.LoadingContentBundle => "connecting-update-status-loading-content-bundle",
                _ => "connecting-update-status-unknown"
            }))) + _reasonSuffix,
            Connector.ConnectionStatus.Connecting => _loc.GetString("connecting-status-connecting") + _reasonSuffix,
            Connector.ConnectionStatus.ConnectionFailed => _loc.GetString("connecting-status-connection-failed"),
            Connector.ConnectionStatus.StartingClient => _loc.GetString("connecting-status-starting-client") + _reasonSuffix,
            Connector.ConnectionStatus.NotAContentBundle => _loc.GetString("connecting-status-not-a-content-bundle"),
            Connector.ConnectionStatus.ClientExited => _connector.ClientExitedBadly
                ? _loc.GetString("connecting-status-client-crashed")
                : "",
            _ => ""
        };

    private string FormatUpdateError()
    {
        return _updater.UpdateException switch
        {
            NoEngineForPlatformException => _loc.GetString("connecting-status-update-error-no-engine-for-platform"),
            NoModuleForPlatformException => _loc.GetString("connecting-status-update-error-no-module-for-platform"),
            _ => _loc.GetString("connecting-status-update-error",
                ("err", _updater.UpdateException?.Message ?? _loc.GetString("connecting-status-update-error-unknown")))
        };
    }

    public string TitleText => _connectionType switch
    {
        ConnectionType.Server => _loc.GetString("connecting-title-connecting"),
        ConnectionType.ContentBundle => _loc.GetString("connecting-title-content-bundle"),
        _ => ""
    };

    public bool IsAskingPrivacyPolicy => _connectorStatus == Connector.ConnectionStatus.AwaitingPrivacyPolicyAcceptance;

    public string PrivacyPolicyText => _connector.PrivacyPolicyDifferentVersion
        ? _loc.GetString("connecting-privacy-policy-text-version-changed")
        : _loc.GetString("connecting-privacy-policy-text");

    public static void StartConnect(MainWindowViewModel windowVm, string address, string? givenReason = null)
    {
        var connector = new Connector();
        var vm = new ConnectingViewModel(connector, windowVm, givenReason, ConnectionType.Server);
        windowVm.ConnectingVM = vm;
        vm.Start(address);
        StartedConnecting?.Invoke();
    }

    public static void StartContentBundle(MainWindowViewModel windowVm, IStorageFile file)
    {
        var connector = new Connector();
        var vm = new ConnectingViewModel(connector, windowVm, null, ConnectionType.ContentBundle);
        windowVm.ConnectingVM = vm;
        vm.StartContentBundle(file);
        StartedConnecting?.Invoke();
    }

    private void Start(string address)
    {
        _connector.Connect(address, _cancelSource.Token);
    }

    private void StartContentBundle(IStorageFile file)
    {
        _connector.LaunchContentBundle(file, _cancelSource.Token);
    }

    public void ErrorDismissed()
    {
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        _windowVm.ConnectingVM = null;
    }

    public void Cancel()
    {
        _cancelSource.Cancel();
    }

    public void PrivacyPolicyView()
    {
        Helpers.SafeOpenServerUri(_connector.PrivacyPolicyInfo!.Link);
    }

    public void PrivacyPolicyAccept()
    {
        _connector.ConfirmPrivacyPolicy(PrivacyPolicyAcceptResult.Accepted);
    }

    public void PrivacyPolicyDeny()
    {
        _connector.ConfirmPrivacyPolicy(PrivacyPolicyAcceptResult.Denied);
    }

    public enum ConnectionType
    {
        Server,
        ContentBundle
    }
}
