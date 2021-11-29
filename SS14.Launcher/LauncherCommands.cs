using System;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Threading;
using Serilog;
using Splat;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels;

namespace SS14.Launcher;

public class LauncherCommands
{
    private static IDisposable? _timer;
    private static string _reason = "";

    private MainWindowViewModel _windowVm;
    private LoginManager _loginMgr;
    private LauncherMessaging _msgr;
    public readonly Channel<string> CommandChannel;

    public LauncherCommands(MainWindowViewModel windowVm)
    {
        _windowVm = windowVm;
        _loginMgr = Locator.Current.GetRequiredService<LoginManager>();
        _msgr = Locator.Current.GetRequiredService<LauncherMessaging>();

        CommandChannel = Channel.CreateUnbounded<string>();
    }

    private void ActivateWindow()
    {
        // This may not work, but let's try anyway...
        // In particular keep in mind:
        // https://github.com/AvaloniaUI/Avalonia/issues/2398
        _windowVm.Control?.ActivateForReal();
    }

    private async Task Connect(string param)
    {
        var reason = _reason == "" ? null : _reason;
        // Sanity-check the connection.

        LoggedInAccount? activeAccount = null;
        while (true)
        {
            activeAccount = _loginMgr.ActiveAccount;

            if ((activeAccount == null) || (activeAccount.Status == AccountLoginStatus.Unsure))
            {
                await Task.Delay(ConfigConstants.LauncherCommandsRedialWaitTimeout);
            }
            else
            {
                break;
            }
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Log.Warning($"Dropping connect command: Sanity checks have failed (not on UI thread)");
            return;
        }

        if (activeAccount!.Status != AccountLoginStatus.Available)
        {
            Log.Warning($"Dropping connect command: Account not available");
            return;
        }

        // Drop the command if we are already connecting.
        if (_windowVm.ConnectingVM != null)
        {
            Log.Warning($"Dropping connect command: Busy connecting to a server");
            return;
        }
        // Note that we don't want to activate the window for something we'll requeue again and again.
        ActivateWindow();
        Log.Information($"Connect command: \"{param}\", \"{reason}\"");
        ConnectingViewModel.StartConnect(_windowVm, param, reason);
    }

    public async ValueTask QueueCommand(string cmd)
    {
        await CommandChannel.Writer.WriteAsync(cmd);
    }

    public void Shutdown()
    {
        CommandChannel.Writer.Complete();
    }

    public async void RunCommandTask()
    {
        var reader = CommandChannel.Reader;
        while (await reader.WaitToReadAsync())
        {
            var cmd = await reader.ReadAsync();
            try
            {
                await RunSingleCommand(cmd);
            }
            catch (Exception e)
            {
                Log.Error(e, "Exception while processing launcher command {Command}", cmd);
            }
        }
    }

    private async Task RunSingleCommand(string cmd)
    {
        Log.Debug($"Launcher command: {cmd}");

        string? GetUntrustedTextField()
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromHexString(cmd.Substring(1)));
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to parse untrusted text field: {ex}");
                return null;
            }
        }

        if (cmd == PingCommand)
        {
            // Yup!
            ActivateWindow();
        }
        else if (cmd == RedialWaitCommand)
        {
            // Redialling wait
            await Task.Delay(ConfigConstants.LauncherCommandsRedialWaitTimeout);
        }
        else if (cmd.StartsWith("R"))
        {
            // Reason (encoded in UTF-8 and then into hex for safety)
            _reason = GetUntrustedTextField() ?? "";
        }
        else if (cmd.StartsWith("r"))
        {
            // Reason (no encoding)
            _reason = cmd.Substring(1);
        }
        else if (cmd.StartsWith("C"))
        {
            // Uri (encoded in UTF-8 and then into hex for safety)
            var uri = GetUntrustedTextField();
            if (uri != null)
                await Connect(uri);
        }
        else if (cmd.StartsWith("c"))
        {
            // Used by the "pass URI as argument" logic, doesn't need to bother with safety measures
            await Connect(cmd.Substring(1));
        }
        else
        {
            Log.Error($"Unhandled launcher command: {cmd}");
        }
    }

    // Command constructors

    public const string PingCommand = ":Ping";
    public const string RedialWaitCommand = ":RedialWait";
    public const string BlankReasonCommand = "r";
    public static string ConstructConnectCommand(Uri uri) => "c" + uri.ToString();
}

