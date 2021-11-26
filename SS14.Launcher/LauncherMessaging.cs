using System;
using System.Collections.Generic;
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
    /// Initial commands that are fed into the launcher commands system on startup of the launcher.
    /// </summary>
    private string[] _initialCommands = Array.Empty<string>();

    /// <summary>
    /// *The* pipe server stream.
    /// </summary>
    private NamedPipeServerStream? _pipeServer;


    /// <summary>
    /// Either sends a command (a string containing anything except a carriage return or newline) to the primary launcher process,
    ///  or claims the primary launcher process, provides a hook for commands, and then passes the sent command to that hook.
    /// Returns true if the command was sent elsewhere (and the application should shutdown now).
    /// Returns false if we are the primary launcher process.
    /// This function should only ever be called once.
    /// This occurs before Avalonia init.
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
            // Ok, so we're server (we hope)
            Console.WriteLine("We are primary launcher (or primary launcher is out for lunch)");
        }

        // Try to create server
        try
        {
            _pipeServer = new NamedPipeServerStream(actualPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Pipe server: Could not be created: {e}");
        }

        if (sendAnyway)
        {
            _initialCommands = commands;
        }
        return false;
    }

    /// <summary>
    /// This is the actual async server task, responsible for making everything else someone else's problem.
    /// This occurs post-Avalonia-init.
    /// </summary>
    public async Task ServerTask(LauncherCommands lc)
    {
        // Handle initial commands before actually doing server stuff (as there may be no server).
        foreach (string s in _initialCommands)
        {
            await lc.RunCommand(s);
        }
        // Actual server code
        if (_pipeServer == null) return;
        // With the pipe server created, we can move on
        // Note we can't just close the StreamReader per-connection.
        // It would close the underlying pipe server (breaking everything).
        var sr = new StreamReader(_pipeServer, Encoding.UTF8);
        while (true)
        {
            await _pipeServer.WaitForConnectionAsync();
            try
            {
                while (!sr.EndOfStream)
                {
                    await lc.RunCommand(sr.ReadLine());
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

