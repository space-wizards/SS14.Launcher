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

public class LauncherMessaging
{
    /// <summary>
    /// Queued commands. Inbound commands are sent from LauncherMessaging, handled/requeued in LauncherCommands.
    /// </summary>
    public ConcurrentQueue<string> CommandQueue = new();

    private NamedPipeServerStream? _pipeServer;

    /// <summary>
    /// Either sends a command (a string containing anything except a carriage return or newline) to the primary launcher process,
    ///  or claims the primary launcher process, provides a hook for commands, and then passes the sent command to that hook.
    /// Returns true if the command was sent elsewhere (and the application should shutdown now).
    /// Returns false if we are the primary launcher process.
    /// This function should only ever be called once.
    /// </summary>
    /// <param name="command">The sent command.</param>
    /// <param name="sendAnyway">If true, when claimed, the hook is given the command immediately for later processing.</param>
    public bool SendCommandsOrClaim(string[] commands, bool sendAnyway = true)
    {
        // Verify command matches rules
        foreach (var command in commands)
        {
            if (command.Contains('\n') || command.Contains('\r'))
                throw new ArgumentOutOfRangeException(nameof(command), "No newlines are allowed in a launcher IPC command.");
        }

        var actualPipeName = ConfigConstants.LauncherCommandsNamedPipeName + "_" + Convert.ToHexString(Encoding.UTF8.GetBytes(Environment.UserName));

        // Must use Console since we are in pre-init context. Better than nothing if this somehow misdetects.

        // So during testing on Linux, I found that NamedPipeServerStream does NOT have it's "mutex" semantics.
        // Don't know who to blame for this, don't care, let's just try connecting first.
        try
        {
            using (var client = new NamedPipeClientStream(actualPipeName))
            {
                // If we are waiting more than 5 seconds something has gone HORRIBLY wrong and we should just let the launcher start.
                client.Connect(ConfigConstants.LauncherCommandsNamedPipeTimeout);
                // Command is newline-terminated.
                byte[] commandEncoded = Encoding.UTF8.GetBytes(string.Join('\n', commands) + "\n");
                client.Write(commandEncoded);
            }
            Console.WriteLine("Passed commands to primary launcher");
            return true;
        }
        catch (Exception)
        {
            // Ok, so we're server.
            Console.WriteLine("We are primary launcher (or primary launcher is out for lunch)");
        }
        // Try to create server
        try
        {
            _pipeServer = new NamedPipeServerStream(actualPipeName);
            new Thread(() =>
            {
                try
                {
                    // Note we can't just close the StreamReader per-connection.
                    // It would close the underlying pipe server (breaking everything).
                    var sr = new StreamReader(_pipeServer, Encoding.UTF8);
                    while (true)
                    {
                        _pipeServer.WaitForConnection();
                        try
                        {
                            while (!sr.EndOfStream)
                            {
                                CommandQueue.Enqueue(sr.ReadLine());
                            }
                        }
                        catch (Exception e)
                        {
                            // Not much we can do here.
                            Console.WriteLine("Pipe server: Unexpected end of stream.");
                        }
                        try
                        {
                            _pipeServer.Disconnect();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                catch (Exception e)
                {
                    // Thrown when the pipeServer gets disposed.
                    // Done on purpose so main thread can terminate the pipe server.
                    // Note that ObjectDisposedException is not necessarily thrown!!!
                    Console.WriteLine($"Pipe server: Shutting down, cause {e}");
                }
            }).Start();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Pipe server: Could not be created: {e}");
        }

        if (sendAnyway)
        {
            foreach (var command in commands)
            {
                CommandQueue.Enqueue(command);
            }
        }
        return false;
    }

    /// <summary>
    /// Closes the pipe server remotely, which causes WaitForConnection to throw, which cleans up the pipe server thread.
    /// This is important because otherwise the thread sticks around.
    /// </summary>
    public void ShutdownPipeServer()
    {
        if (_pipeServer != null)
        {
            _pipeServer.Close();
        }
    }
}

