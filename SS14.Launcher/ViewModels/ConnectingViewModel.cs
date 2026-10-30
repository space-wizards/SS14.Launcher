using System;
using System.Threading;
using Avalonia.Platform.Storage;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.Utility;
using static SS14.Launcher.Models.Connector.ConnectionStatus;

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
    private bool _failureShown;

    public bool IsErrored
        => _connector.Status == ConnectionFailed ||
           _connector.Status == UpdateError ||
           _connector.Status == NotAContentBundle ||
           _connector is { Status: ClientExited, ClientExitedBadly: true };

    public static event Action? StartedConnecting;

    public ConnectingViewModel(Connector connector, MainWindowViewModel windowVm, string? givenReason, ConnectionType connectionType)
    {
        _updater = Locator.Current.GetRequiredService<Updater>();
        _loc = LocalizationManager.Instance;
        _connector = connector;
        _windowVm = windowVm;
        _connectionType = connectionType;
        _reasonSuffix = (givenReason != null) ? ("\n" + givenReason) : "";

        _updater.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(_updater.Progress):
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(ProgressIndeterminate));
                    OnPropertyChanged(nameof(ProgressText));
                    break;

                case nameof(_updater.ProgressBytes):
                    OnPropertyChanged(nameof(ProgressText));
                    break;

                case nameof(_updater.Speed):
                    OnPropertyChanged(nameof(SpeedText));
                    OnPropertyChanged(nameof(SpeedIndeterminate));
                    break;

                case nameof(_updater.Status):
                    OnPropertyChanged(nameof(StatusText));
                    break;
            }
        };

        _connector.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(_connector.Status):
                    OnPropertyChanged(nameof(ProgressIndeterminate));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(ProgressBarVisible));
                    OnPropertyChanged(nameof(IsErrored));
                    OnPropertyChanged(nameof(IsAskingPrivacyPolicy));

                    if (_connector.Status == ClientRunning
                        || _connector.Status == Cancelled
                        || _connector is { Status: ClientExited, ClientExitedBadly: false })
                    {
                        CloseOverlay();
                    }
                    else if (IsErrored)
                    {
                        ShowFailure();
                    }

                    break;
                case nameof(_connector.PrivacyPolicyDifferentVersion):
                    OnPropertyChanged(nameof(PrivacyPolicyText));
                    break;
                case nameof(_connector.ClientExitedBadly):
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(IsErrored));
                    break;
            }
        };
    }

    public float Progress
    {
        get
        {
            if (_updater.Progress == null)
            {
                return 0;
            }

            var (downloaded, total, _) = _updater.Progress.Value;

            return downloaded / (float)total;
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

            var (downloaded, total, unit) = _updater.Progress.Value;

            return unit switch
            {
                Updater.ProgressUnit.Bytes => $"{Helpers.FormatBytes(downloaded)} / {Helpers.FormatBytes(total)}",
                // A count (of files). Say how much came in too, if we know.
                _ => _updater.ProgressBytes is { } bytes
                    ? $"{downloaded} / {total} ({Helpers.FormatBytes(bytes)})"
                    : $"{downloaded} / {total}"
            };
        }
    }

    public bool ProgressIndeterminate
        => _connector.Status != Updating
           || _updater.Progress == null;

    public bool ProgressBarVisible
        => _connector.Status != ClientExited &&
           _connector.Status != ClientRunning &&
           _connector.Status != ConnectionFailed &&
           _connector.Status != UpdateError &&
           _connector.Status != NotAContentBundle;

    public bool SpeedIndeterminate => _connector.Status != Updating || _updater.Speed == null;

    public string SpeedText
    {
        get
        {
            if (_updater.Speed is not { } speed)
                return "";

            return $"{Helpers.FormatBytes(speed)}/s";
        }
    }

    public string StatusText
        => _connector.Status switch
        {
            None => _loc.GetString("connecting-status-none") + _reasonSuffix,
            UpdateError => FormatUpdateError(),
            Updating => _loc.GetString("connecting-status-updating", ("status", _loc.GetString(_updater.Status switch
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
            Connecting => _loc.GetString("connecting-status-connecting") + _reasonSuffix,
            ConnectionFailed => _loc.GetString("connecting-status-connection-failed"),
            StartingClient => _loc.GetString("connecting-status-starting-client") + _reasonSuffix,
            NotAContentBundle => _loc.GetString("connecting-status-not-a-content-bundle"),
            ClientExited => _connector.ClientExitedBadly
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
            { } e => _loc.GetString("connecting-status-update-error", ("err", $"{e.GetType().Name}: {e.Message}")),
            _ => _loc.GetString("connecting-status-update-error",
                ("err", _loc.GetString("connecting-status-update-error-unknown")))
        };
    }

    // The error code line for the current failure, including the exit code if the game exited.
    private string ErrorCodeText
    {
        get
        {
            if (!IsErrored || _connector.FailureCode is not { } code)
                return "";

            if (_connector.ClientExitCode is { } exitCode)
            {
                if (!OperatingSystem.IsWindows() && exitCode is > 128 and <= 192)
                {
                    return _loc.GetString("error-code-label-exit-signal",
                               ("code", code.ToString()), ("exit", exitCode), ("signal", exitCode - 128))
                           + "\n" + _loc.GetString("error-code-help");
                }

                return _loc.GetString("error-code-label-exit-code",
                           ("code", code.ToString()), ("exit", exitCode))
                       + "\n" + _loc.GetString("error-code-help");
            }

            return _loc.GetString("error-code-label", ("code", code.ToString()))
                   + "\n" + _loc.GetString("error-code-help");
        }
    }

    public string TitleText => _connectionType switch
    {
        ConnectionType.Server => _loc.GetString("connecting-title-connecting"),
        ConnectionType.ContentBundle => _loc.GetString("connecting-title-content-bundle"),
        _ => ""
    };

    public bool IsAskingPrivacyPolicy => _connector.Status == AwaitingPrivacyPolicyAcceptance;

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

    /// <summary>
    /// Replaces this screen with the regular error overlay (title, message, hint, code, OK button).
    /// </summary>
    private void ShowFailure()
    {
        if (_failureShown)
            return;

        _failureShown = true;

        var titleKey = _connectionType == ConnectionType.ContentBundle
            ? "error-overlay-title-content-bundle-failed"
            : "error-overlay-title-connect-failed";

        // "Failed to connect to server!" would only repeat the title, so it can go away >:(
        var message = _connector.Status == ConnectionFailed ? "" : StatusText;
        var code = _connector.FailureCode ?? ErrorCodes.GenUnexpected;
        var codeText = ErrorCodeText;

        CloseOverlay();
        _windowVm.ShowFailureOverlay(_loc.GetString(titleKey), message, code, codeText);
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
