using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Serilog;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class DevelopmentTabViewModel : MainWindowTabViewModel
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly DataManager _cfg = Locator.Current.GetRequiredService<DataManager>();
    private readonly MainWindowViewModel? _windowVm;

    private DevFakeServer? _fakeServer;
    private DevelopmentFailure? _selectedFailure;

    // Used by the XAML designer.
    public DevelopmentTabViewModel() : this(null)
    {
    }

    public DevelopmentTabViewModel(MainWindowViewModel? windowVm)
    {
        _windowVm = windowVm;

        // TODO: This sucks and leaks.
        _cfg.GetCVarEntry(CVars.EngineOverrideEnabled).PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Name));
        };

        Failures = BuildFailures();
        _selectedFailure = Failures[0];
    }

    /// <summary>
    /// Ways to make the launcher fail, to see what the user would see.
    /// </summary>
    public IReadOnlyList<DevelopmentFailure> Failures { get; }

    public DevelopmentFailure? SelectedFailure
    {
        get => _selectedFailure;
        set => SetProperty(ref _selectedFailure, value);
    }

    public void TriggerFailure()
    {
        SelectedFailure?.Run();
    }

    private DevelopmentFailure[] BuildFailures()
    {
        return
        [
            // Exceptions nothing catches.
            new("Unhandled: throw in a button handler (overlay)", () =>
                throw new InvalidOperationException("Test exception from a button handler")),
            new("Unhandled: throw in an async void method (overlay)", ThrowInAsyncVoid),
            new("Unhandled: fault a background task (only logged)", FaultBackgroundTask),
            new("Unhandled: throw on a new thread (CRASHES the launcher)", () =>
                new Thread(() => throw new InvalidOperationException("Test exception from a new thread")).Start()),

            // Exceptions the classifier turns into a code, shown in an overlay.
            new("Classify: file in use", () => Classify(new IOException("File is in use",
                OperatingSystem.IsWindows() ? unchecked((int)0x80070020) : 16))),
            new("Classify: disk full", () => Classify(new IOException("Disk is full",
                OperatingSystem.IsWindows() ? unchecked((int)0x80070070) : 28))),
            new("Classify: permission denied", () => Classify(new UnauthorizedAccessException("Access denied"))),
            new("Classify: path too long", () => Classify(new PathTooLongException())),
            new("Classify: database busy", () => Classify(new SqliteException("database is locked", 5))),
            new("Classify: database can't be opened", () => Classify(new SqliteException("unable to open", 14))),
            new("Classify: host not found", () => Classify(new HttpRequestException("Request failed",
                new SocketException((int)SocketError.HostNotFound)))),
            new("Classify: connection refused", () => Classify(new HttpRequestException("Request failed",
                new SocketException((int)SocketError.ConnectionRefused)))),
            new("Classify: TLS failure", () => Classify(new HttpRequestException("Request failed",
                new AuthenticationException("The remote certificate is invalid")))),
            new("Classify: request timeout", () => Classify(
                new TaskCanceledException("Timed out", new TimeoutException()))),
            new("Classify: missing native library", () => Classify(new DllNotFoundException("libsodium"))),
            new("Classify: corrupt download", () => Classify(new InvalidDataException("Corrupt data"))),

            // The overlays other parts of the launcher show, with their real text.
            new("Overlay: offline", () => ShowOverlay(
                "error-overlay-title-offline", "error-overlay-desc-offline", ErrorCodes.NetNoConnection)),
            new("Overlay: settings could not be saved", () => ShowOverlay(
                "error-overlay-title-save-settings", "error-overlay-desc-save-settings", ErrorCodes.DbSaveSettings)),
            new("Overlay: unexpected error", () => ShowOverlay(
                "error-overlay-title-unexpected", "error-overlay-desc-unexpected", ErrorCodes.GenBackground)),
            new("Overlay: could not clear content", () => ShowOverlay(
                "error-overlay-title-clear-content", "error-overlay-desc-clear-content", ErrorCodes.DbClearContent)),
            new("Overlay: could not clear engines", () => ShowOverlay(
                "error-overlay-title-clear-engines", "error-overlay-desc-clear-engines", ErrorCodes.IoClearEngines)),
            new("Overlay: could not open a link", () => ShowOverlay(
                "error-overlay-title-open-external", "error-overlay-desc-open-external", ErrorCodes.IoOpenExternal)),
            new("Overlay: running from a translocated location", () => ShowOverlay(
                "error-overlay-title-translocated", "error-overlay-desc-translocated", ErrorCodes.GenTranslocated)),

            // Real connects, because thats the easiest way to do this
            new("Connect: nothing is listening (refused)", () => Connect("ss14://127.0.0.1:1")),
            new("Connect: unknown host", () => Connect("ss14://this-host-does-not-exist.invalid")),
            new("Connect: address that isn't one", () => Connect("http://not-an-ss14-address")),
            new("Connect: server sends broken JSON", () => ConnectToFakeServer(DevFakeServerMode.BadJson)),
            new("Connect: server sends an empty answer", () => ConnectToFakeServer(DevFakeServerMode.NullJson)),
            new("Connect: server answers 404", () => ConnectToFakeServer(DevFakeServerMode.NotFound)),
            new("Connect: server hangs up mid-answer", () => ConnectToFakeServer(DevFakeServerMode.Truncated)),
        ];
    }

    private static async void ThrowInAsyncVoid()
    {
        await Task.Yield();
        throw new InvalidOperationException("Test exception from an async void method");
    }

    private static void FaultBackgroundTask()
    {
        StartFaultingTask();

        // Unobserved task exceptions are only reported when the task is finalized, so make that happen.
        _ = Task.Delay(200).ContinueWith(_ =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        });
    }

    private static void StartFaultingTask()
    {
        _ = Task.Run(() => throw new InvalidOperationException("Test exception from a background task"));
    }

    private void Classify(Exception e)
    {
        var code = ErrorClassifier.Classify(e);
        Log.Information(e, "Development tab: classified a test exception as {ErrorCode}", code);

        ShowOverlay("error-overlay-title-unexpected", "error-overlay-desc-unexpected", code);
    }

    private void ShowOverlay(string titleKey, string descriptionKey, ErrorCode code)
    {
        _windowVm?.ShowErrorOverlay(titleKey, descriptionKey, code);
    }

    private void Connect(string address)
    {
        if (_windowVm != null)
            ConnectingViewModel.StartConnect(_windowVm, address);
    }

    private void ConnectToFakeServer(DevFakeServerMode mode)
    {
        _fakeServer?.Dispose();
        _fakeServer = new DevFakeServer(mode, TimeSpan.FromMinutes(2));

        Connect($"ss14://127.0.0.1:{_fakeServer.Port}");
    }

    public override string Name
        => _cfg.GetCVar(CVars.EngineOverrideEnabled)
        ? _loc.GetString("tab-development-title-override")
        : _loc.GetString("tab-development-title");

    public bool DisableSigning
    {
        get => _cfg.GetCVar(CVars.DisableSigning);
        set
        {
            _cfg.SetCVar(CVars.DisableSigning, value);
            _cfg.CommitConfig();
        }
    }

    public bool EngineOverrideEnabled
    {
        get => _cfg.GetCVar(CVars.EngineOverrideEnabled);
        set
        {
            _cfg.SetCVar(CVars.EngineOverrideEnabled, value);
            _cfg.CommitConfig();
        }
    }

    public string EngineOverridePath
    {
        get => _cfg.GetCVar(CVars.EngineOverridePath);
        set
        {
            _cfg.SetCVar(CVars.EngineOverridePath, value);
            _cfg.CommitConfig();
        }
    }
}
