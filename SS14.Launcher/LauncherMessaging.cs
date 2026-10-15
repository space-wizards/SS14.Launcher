using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

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
    /// Cancellation token used to safely shut down the pipe server.
    /// </summary>
    private readonly CancellationTokenSource _pipeServerSelfDestruct = new();

    private Task? _serverTask;

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

        var actualPipeName = ConfigConstants.LauncherCommandsNamedPipeName;

        // NOTE: On Unix, NamedPipeStream allows passing full rooted file paths.
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { } runtimeDir && !string.IsNullOrEmpty(runtimeDir))
        {
            // Use XDG_RUNTIME_DIR to store the pipe if available.
            actualPipeName = Path.Combine(runtimeDir, actualPipeName);
        }
        else if (!OperatingSystem.IsMacOS())
        {
            // Pipes use TMPDIR on macOS which is user-specific, so we don't need to give them a funny name to avoid multi-user problems.
            // On other platforms, throw the user name along with the pipe name to avoid any conflicts.
            actualPipeName += "_" + Convert.ToHexString(Encoding.UTF8.GetBytes(Environment.UserName));
        }

        // Must use Console since we are in pre-init context. Better than nothing if this somehow misdetects.

        // So during testing on Linux, I found that NamedPipeServerStream does NOT have it's "mutex" semantics.
        // Don't know who to blame for this, don't care, let's just try connecting first.
        try
        {
            using (var client = new NamedPipeClientStream(".", actualPipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly))
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
            Console.WriteLine("We are the primary launcher (or primary launcher is out for lunch)");
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

    public void StartServerTask(LauncherCommands lc)
    {
        _serverTask = ServerTask(lc);
    }

    public void StopAndWait()
    {
        _pipeServerSelfDestruct.Cancel();
        _serverTask?.Wait();
    }

    /// <summary>
    /// This is the actual async server task, responsible for making everything else someone else's problem.
    /// This occurs post-Avalonia-init.
    /// </summary>
    private async Task ServerTask(LauncherCommands lc)
    {
        var token = _pipeServerSelfDestruct.Token;
        // Handle initial commands before actually doing server stuff (as there may be no server).
        foreach (string s in _initialCommands)
        {
            await lc.QueueCommand(s);
        }

        // Actual server code
        if (_pipeServer == null) return;
        // With the pipe server created, we can move on
        // Note we can't just close the StreamReader per-connection.
        // It would close the underlying pipe server (breaking everything).
        var sr = new StreamReader(_pipeServer, Encoding.UTF8);
        try
        {
            while (true)
            {
                await _pipeServer.WaitForConnectionAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;
                try
                {
                    while (!sr.EndOfStream)
                    {
                        // Can't be cancelled
                        var line = await sr.ReadLineAsync().WaitAsync(token).ConfigureAwait(false);
                        if (line != null)
                            await lc.QueueCommand(line);
                    }

                    _pipeServer.Disconnect();
                }
                catch (OperationCanceledException)
                {
                    // Rethrow outside loop.
                    throw;
                }
                catch (Exception e)
                {
                    // Not much we can do here.
                    Log.Warning($"Pipe server: Exception during a connection: {e}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // We're gucci
        }
        finally
        {
            await _pipeServer.DisposeAsync();
        }
    }
}

