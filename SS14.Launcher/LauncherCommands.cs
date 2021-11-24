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
                    var requeue = false;
                    if (cmd == PingCommand)
                    {
                        // Success!
                    }
                    else if (cmd.StartsWith("U"))
                    {
                        // Sanity-check this!!!
                        var activeAccount = loginMgr.ActiveAccount;
                        if ((activeAccount == null) || (activeAccount.Status != AccountLoginStatus.Available))
                        {
                            requeue = true;
                        }
                        else
                        {
                            var uriStr = cmd.Substring(1);
                            ConnectingViewModel.StartConnect(windowVm, uriStr);
                        }
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
    public static string ConstructConnectCommand(Uri uri) => "U" + uri.ToString();
}

