using System;
using System.Reactive.Linq;
using System.Threading;
using ReactiveUI;
using Splat;
using SS14.Launcher.Models;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels;

public class ConnectingViewModel : ViewModelBase
{
    private readonly Connector _connector;
    private readonly Updater _updater;
    private readonly MainWindowViewModel _windowVm;

    private readonly CancellationTokenSource _cancelSource = new CancellationTokenSource();

    private string? _reasonSuffix;

    private Connector.ConnectionStatus _connectorStatus;
    private Updater.UpdateStatus _updaterStatus;
    private (long downloaded, long total, Updater.ProgressUnit unit)? _updaterProgress;
    private long? _updaterSpeed;

    public bool IsErrored => _connectorStatus == Connector.ConnectionStatus.ConnectionFailed ||
                             _connectorStatus == Connector.ConnectionStatus.UpdateError ||
                             _connectorStatus == Connector.ConnectionStatus.ClientExited &&
                             _connector.ClientExitedBadly;

    public static event Action? StartedConnecting;

    public ConnectingViewModel(Connector connector, MainWindowViewModel windowVm, string? givenReason)
    {
        _updater = Locator.Current.GetRequiredService<Updater>();
        _connector = connector;
        _windowVm = windowVm;
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

                if (val == Connector.ConnectionStatus.ClientRunning
                    || val == Connector.ConnectionStatus.Cancelled
                    || val == Connector.ConnectionStatus.ClientExited && !_connector.ClientExitedBadly)
                {
                    CloseOverlay();
                }
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
                                      _connectorStatus != Connector.ConnectionStatus.UpdateError;

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
            Connector.ConnectionStatus.None => "Starting connection..." + _reasonSuffix,
            Connector.ConnectionStatus.UpdateError =>
                "There was an error while downloading server content. If this persists try some of the following:" +
                "\n1. Try connecting to another game server to see if the problem persists." +
                "\n2. Try enabling a VPN, or disabling it if you already have one active." +
                "\n3. If all else fails, get support from the server you are attempting to join.",
            Connector.ConnectionStatus.Updating => ("Updating: " + _updaterStatus switch
            {
                Updater.UpdateStatus.CheckingClientUpdate => "Checking for server content update...",
                Updater.UpdateStatus.DownloadingEngineVersion => "Downloading server content...",
                Updater.UpdateStatus.DownloadingClientUpdate => "Downloading server content...",
                Updater.UpdateStatus.FetchingClientManifest => "Fetching server manifest...",
                Updater.UpdateStatus.Verifying => "Verifying download integrity...",
                Updater.UpdateStatus.CullingEngine => "Clearing old content...",
                Updater.UpdateStatus.CullingContent => "Clearing old server content...",
                Updater.UpdateStatus.Ready => "Update done!",
                Updater.UpdateStatus.CheckingEngineModules => "Checking for additional dependencies...",
                Updater.UpdateStatus.DownloadingEngineModules => "Downloading extra dependencies...",
                Updater.UpdateStatus.CommittingDownload => "Synchronizing to disk...",
                Updater.UpdateStatus.LoadingIntoDb => "Storing assets in database...",
                Updater.UpdateStatus.LoadingContentBundle => "Loading content bundle...",
                _ => "You shouldn't see this"
            }) + _reasonSuffix,
            Connector.ConnectionStatus.Connecting => "Fetching connection info from server..." + _reasonSuffix,
            Connector.ConnectionStatus.ConnectionFailed => "Failed to connect to server!",
            Connector.ConnectionStatus.StartingClient => "Starting client..." + _reasonSuffix,
            Connector.ConnectionStatus.NotAContentBundle => "File is not a valid content bundle!",
            Connector.ConnectionStatus.ClientExited => _connector.ClientExitedBadly
                ? "Client seems to have crashed while starting. If this persists, please ask on Discord or GitHub for support."
                : "",
            _ => ""
        };

    public static void StartConnect(MainWindowViewModel windowVm, string address, string? givenReason = null)
    {
        var connector = new Connector();
        var vm = new ConnectingViewModel(connector, windowVm, givenReason);
        windowVm.ConnectingVM = vm;
        vm.Start(address);
        StartedConnecting?.Invoke();
    }

    public static void StartContentBundle(MainWindowViewModel windowVm, string fileName)
    {
        var connector = new Connector();
        var vm = new ConnectingViewModel(connector, windowVm, null);
        windowVm.ConnectingVM = vm;
        vm.StartContentBundle(fileName);
        StartedConnecting?.Invoke();
    }

    private void Start(string address)
    {
        _connector.Connect(address, _cancelSource.Token);
    }

    private void StartContentBundle(string fileName)
    {
        _connector.LaunchContentBundle(fileName, _cancelSource.Token);
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
}
