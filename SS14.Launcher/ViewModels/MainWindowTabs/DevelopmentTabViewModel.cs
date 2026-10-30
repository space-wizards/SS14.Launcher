using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Text.RegularExpressions;
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
        List<DevelopmentFailure> failures =
        [
            // Exceptions nothing catches. The UI handler gives these GEN-003, except the thread, which is a crash.
            new($"{Short(ErrorCodes.GenBackground)}: Unhandled: throw in a button handler (overlay)", () =>
                throw new InvalidOperationException("Test exception from a button handler")),
            new($"{Short(ErrorCodes.GenBackground)}: Unhandled: throw in an async void method (overlay)",
                ThrowInAsyncVoid),
            new($"{Short(ErrorCodes.GenBackground)}: Unhandled: fault a background task (only logged)",
                FaultBackgroundTask),
            new($"{Short(ErrorCodes.GenCrash)}: Unhandled: throw on a new thread (CRASHES the launcher)", () =>
                new Thread(() => throw new InvalidOperationException("Test exception from a new thread")).Start()),

            // Exceptions the classifier turns into a code, shown in an overlay.
            ClassifyEntry("file in use", new IOException("File is in use",
                OperatingSystem.IsWindows() ? unchecked((int)0x80070020) : 16)),
            ClassifyEntry("disk full", new IOException("Disk is full",
                OperatingSystem.IsWindows() ? unchecked((int)0x80070070) : 28)),
            ClassifyEntry("permission denied", new UnauthorizedAccessException("Access denied")),
            ClassifyEntry("path too long", new PathTooLongException()),
            ClassifyEntry("database busy", new SqliteException("database is locked", 5)),
            ClassifyEntry("database can't be opened", new SqliteException("unable to open", 14)),
            ClassifyEntry("host not found", new HttpRequestException("Request failed",
                new SocketException((int)SocketError.HostNotFound))),
            ClassifyEntry("connection refused", new HttpRequestException("Request failed",
                new SocketException((int)SocketError.ConnectionRefused))),
            ClassifyEntry("TLS failure", new HttpRequestException("Request failed",
                new AuthenticationException("The remote certificate is invalid"))),
            ClassifyEntry("request timeout", new TaskCanceledException("Timed out", new TimeoutException())),
            ClassifyEntry("missing native library", new DllNotFoundException("libsodium")),
            ClassifyEntry("corrupt download", new InvalidDataException("Corrupt data")),

            // The overlays other parts of the launcher show, with their real text.
            OverlayEntry("offline", "offline", ErrorCodes.NetNoConnection),
            OverlayEntry("settings could not be saved", "save-settings", ErrorCodes.DbSaveSettings),
            OverlayEntry("unexpected error", "unexpected", ErrorCodes.GenBackground),
            OverlayEntry("could not clear content", "clear-content", ErrorCodes.DbClearContent),
            OverlayEntry("could not clear engines", "clear-engines", ErrorCodes.IoClearEngines),
            OverlayEntry("could not open a link", "open-external", ErrorCodes.IoOpenExternal),
            OverlayEntry("running from a translocated location", "translocated", ErrorCodes.GenTranslocated),

            // Real connects, because thats the easiest way to do this. The code depends on what happens.
            new("Connect: nothing is listening (refused)", () => Connect("ss14://127.0.0.1:1")),
            new("Connect: unknown host", () => Connect("ss14://this-host-does-not-exist.invalid")),
            new("Connect: address that isn't one", () => Connect("http://not-an-ss14-address")),
            new("Connect: server sends broken JSON", () => ConnectToFakeServer(DevFakeServerMode.BadJson)),
            new("Connect: server sends an empty answer", () => ConnectToFakeServer(DevFakeServerMode.NullJson)),
            new("Connect: server answers 404", () => ConnectToFakeServer(DevFakeServerMode.NotFound)),
            new("Connect: server hangs up mid-answer", () => ConnectToFakeServer(DevFakeServerMode.Truncated)),
        ];

        failures.AddRange(BuildHintFailures());

        return failures.ToArray();
    }

    /// <summary>
    /// One entry for every error code that has a hint in the locale files, to see its hint. Found by looking, so
    /// this keeps up with new codes and hints on its own.
    /// </summary>
    private IEnumerable<DevelopmentFailure> BuildHintFailures()
    {
        return typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ErrorCode))
            .Select(f => (Name: f.Name, Code: (ErrorCode)f.GetValue(null)!))
            .Where(c => ErrorHints.Get(c.Code, _loc) != null)
            .OrderBy(c => c.Code.Area)
            .ThenBy(c => c.Code.Number)
            .Select(c => new DevelopmentFailure(
                $"{Short(c.Code)}: Hint, {Humanize(c.Name, c.Code.Area)}",
                // Thrown out of the button handler, so it goes through the real crash handling and the overlay.
                () => throw new LauncherException($"Test exception carrying {c.Code}", c.Code)));
    }

    // The code without its SS14L prefix, as in NET-007.
    private static string Short(ErrorCode code) => $"{code.Area}-{code.Number:D3}";

    // "NetDnsFailed" in the NET area becomes "Dns failed".
    private static string Humanize(string fieldName, string area)
    {
        var name = fieldName.StartsWith(area, StringComparison.OrdinalIgnoreCase)
            ? fieldName[area.Length..]
            : fieldName;

        var words = Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ").ToLowerInvariant();

        return words.Length == 0 ? fieldName : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private DevelopmentFailure ClassifyEntry(string name, Exception e)
    {
        // What it classifies as depends on the operating system, so ask now.
        var code = ErrorClassifier.Classify(e);

        return new DevelopmentFailure($"{Short(code)}: Classify: {name}", () => Classify(e));
    }

    private DevelopmentFailure OverlayEntry(string name, string stringName, ErrorCode code)
    {
        return new DevelopmentFailure(
            $"{Short(code)}: Overlay: {name}",
            () => ShowOverlay($"error-overlay-title-{stringName}", $"error-overlay-desc-{stringName}", code));
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
