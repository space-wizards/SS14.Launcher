using System;
using System.Collections.Concurrent;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SS14.Launcher;

public static class LauncherMessaging
{
    /// <summary>
    /// Queued commands. Inbound commands are sent from LauncherMessaging, handled/requeued in LauncherCommands.
    /// </summary>
    public static ConcurrentQueue<string> CommandQueue = new();

    /// <summary>
    /// Either sends a command (a string of arbitrary contents) to the primary launcher process,
    ///  or claims the primary launcher process, provides a hook for commands, and then passes the sent command to that hook.
    /// Returns true if the command was sent elsewhere (and the application should shutdown now).
    /// Returns false if we are the primary launcher process.
    /// This function should only ever be called once.
    /// </summary>
    /// <param name="command">The sent command.</param>
    /// <param name="sendAnyway">If true, when claimed, the hook is given the command immediately for later processing.</param>
    public static bool SendCommandOrClaim(string command, bool sendAnyway = true)
    {
        // TODO: Inter-launcher sending
        if (sendAnyway) CommandQueue.Enqueue(command);
        return false;
    }
}

