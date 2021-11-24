using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Serilog;
using SS14.Launcher.ViewModels;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher;

public static class LauncherCommands
{
    private static IDisposable? _timer;
    private static string _reason = "";

    /// <summary>
    /// Starts the timer that handles commands and either defers them (connection commands when not ready) or handles them.
    /// </summary>
    public static void StartReceivingTimer(MainWindowViewModel windowVm, LoginManager loginMgr)
    {
        _timer = DispatcherTimer.Run(() =>
        {
            async void Impl()
            {
                if (LauncherMessaging.CommandQueue.TryDequeue(out var cmd))
                {
                    void ActivateWindow()
                    {
                        // This may not work, but let's try anyway...
                        // In particular keep in mind:
                        // https://github.com/AvaloniaUI/Avalonia/issues/2398
                        windowVm.Control?.Activate();
                    }

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

                    bool Connect(string param)
                    {
                        // Sanity-check this!!!
                        var activeAccount = loginMgr.ActiveAccount;
                        if ((activeAccount == null) || (activeAccount.Status != AccountLoginStatus.Available))
                        {
                            return true;
                        }
                        else
                        {
                            // Drop the command if we are already connecting.
                            if (windowVm.ConnectingVM != null)
                                return false;
                            // Note that we don't want to activate the window for something we'll requeue again and again.
                            ActivateWindow();
                            ConnectingViewModel.StartConnect(windowVm, param, _reason == "" ? null : _reason);
                        }
                        return false;
                    }

                    var requeue = false;
                    if (cmd == PingCommand)
                    {
                        // Yup!
                        ActivateWindow();
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
                        if (uri == null)
                            requeue = Connect(uri);
                    }
                    else if (cmd.StartsWith("c"))
                    {
                        // Used by the "pass URI as argument" logic, doesn't need to bother with safety measures
                        requeue = Connect(cmd.Substring(1));
                    }
                    else
                    {
                        Log.Error($"Unhandled launcher command: {cmd}");
                    }
                    if (requeue)
                    {
                        // Command must be re-queued (maybe the user needs to login first)
                        LauncherMessaging.CommandQueue.Enqueue(cmd);
                    }
                }
            }

            Impl();
            return true;
        }, ConfigConstants.CommandQueueCheckInterval, DispatcherPriority.Background);
    }

    // Command constructors

    public const string PingCommand = ":Ping";
    public const string BlankReasonCommand = "r";
    public static string ConstructConnectCommand(Uri uri) => "c" + uri.ToString();
}

