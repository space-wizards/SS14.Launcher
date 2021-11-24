using System;
using System.Threading;
using ReactiveUI;
using Splat;
using SS14.Launcher.Models;

namespace SS14.Launcher.ViewModels;

public class ConnectingViewModel : ViewModelBase
{
    private readonly Connector _connector;
    private readonly Updater _updater;
    private readonly MainWindowViewModel _windowVm;

    private readonly CancellationTokenSource _cancelSource = new CancellationTokenSource();

    private string? _overrideReason;

    public bool IsErrored => _connector.Status == Connector.ConnectionStatus.ConnectionFailed ||
                             _connector.Status == Connector.ConnectionStatus.UpdateError ||
                             _connector.Status == Connector.ConnectionStatus.ClientExited &&
                             _connector.ClientExitedBadly;

    public ConnectingViewModel(Connector connector, MainWindowViewModel windowVm, string? overrideReason)
    {
        _updater = Locator.Current.GetService<Updater>();
        _connector = connector;
        _windowVm = windowVm;
        _overrideReason = overrideReason;

        this.WhenAnyValue(x => x._updater.Progress)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(ProgressIndeterminate));
                this.RaisePropertyChanged(nameof(ProgressText));
            });

        this.WhenAnyValue(x => x._updater.Status)
            .Subscribe(_ => { this.RaisePropertyChanged(nameof(StatusText)); });

        this.WhenAnyValue(x => x._connector.Status)
            .Subscribe(val =>
            {
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
            if (_updater.Progress == null)
            {
                return 0;
            }

            var (downloaded, total) = _updater.Progress.Value;

            return downloaded / (float) total;
        }
    }

    public string ProgressText
    {
        get
        {
            if (_updater.Progress == null)
            {
                return "";
            }

            var (downloaded, total) = _updater.Progress.Value;

            return $"{Helpers.FormatBytes(downloaded)} / {Helpers.FormatBytes(total)}";
        }
    }

    public bool ProgressIndeterminate
    {
        get
        {
            if (_connector.Status == Connector.ConnectionStatus.Updating)
            {
                return !_updater.Progress.HasValue;
            }

            return true;
        }
    }

    public bool ProgressBarVisible => _connector.Status != Connector.ConnectionStatus.ClientExited &&
                                      _connector.Status != Connector.ConnectionStatus.ClientRunning &&
                                      _connector.Status != Connector.ConnectionStatus.ConnectionFailed &&
                                      _connector.Status != Connector.ConnectionStatus.UpdateError;

    public string StatusText =>
        _connector.Status switch
        {
            Connector.ConnectionStatus.None => _overrideReason ?? "Starting connection...",
            Connector.ConnectionStatus.UpdateError =>
                "There was an error while downloading server content. Please ask on Discord for support if the problem persists.",
            Connector.ConnectionStatus.Updating => _overrideReason ?? ("Updating: " + _updater.Status switch
            {
                Updater.UpdateStatus.CheckingClientUpdate => "Checking for server content update...",
                Updater.UpdateStatus.DownloadingEngineVersion => "Downloading server content...",
                Updater.UpdateStatus.DownloadingClientUpdate => "Downloading server content...",
                Updater.UpdateStatus.Verifying => "Verifying download integrity...",
                Updater.UpdateStatus.CullingEngine => "Clearing old content...",
                Updater.UpdateStatus.Ready => "Update done!",
                _ => "You shouldn't see this"
            }),
            Connector.ConnectionStatus.Connecting => _overrideReason ?? "Fetching connection info from server...",
            Connector.ConnectionStatus.ConnectionFailed => "Failed to connect to server!",
            Connector.ConnectionStatus.StartingClient => _overrideReason ?? "Starting client...",
            Connector.ConnectionStatus.ClientExited => _connector.ClientExitedBadly
                ? "Client seems to have crashed while starting. If this persists, please ask on Discord or GitHub for support."
                : "",
            _ => ""
        };

    public static void StartConnect(MainWindowViewModel windowVm, string address, string? overrideReason = null)
    {
        var connector = new Connector();
        var vm = new ConnectingViewModel(connector, windowVm, overrideReason);
        windowVm.ConnectingVM = vm;
        vm.Start(address);
    }

    private void Start(string address)
    {
        _connector.Connect(address, _cancelSource.Token);
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
