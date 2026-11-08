using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SS14.Launcher.Utility;

/// <summary>
/// What a <see cref="DevFakeServer"/> answers every request with.
/// </summary>
public enum DevFakeServerMode
{
    /// <summary>A 200 response whose JSON body is not JSON.</summary>
    BadJson,

    /// <summary>A 200 response whose JSON body is just <c>null</c>.</summary>
    NullJson,

    /// <summary>A 404 response.</summary>
    NotFound,

    /// <summary>A response that promises more data than it sends, then hangs up.</summary>
    Truncated
}

/// <summary>
/// A minimal HTTP server on the loopback interface that answers everything with broken data.
/// Used by the development tab to exercise the launcher's real error handling.
/// </summary>
public sealed class DevFakeServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancel = new();
    private readonly DevFakeServerMode _mode;

    public int Port { get; }

    public DevFakeServer(DevFakeServerMode mode, TimeSpan lifetime)
    {
        _mode = mode;

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _cancel.CancelAfter(lifetime);
        _ = Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        try
        {
            while (!_cancel.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cancel.Token);
                _ = Task.Run(() => HandleClient(client));
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // Shutting down.
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();

                // Read the request up to the blank line that ends the headers, we don't care what it asks for, it's  not getting it anyways.
                var buffer = new byte[4096];
                var total = 0;
                while (total < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(total), _cancel.Token);
                    if (read == 0)
                        return;

                    total += read;
                    if (Encoding.ASCII.GetString(buffer, 0, total).Contains("\r\n\r\n", StringComparison.Ordinal))
                        break;
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes(BuildResponse(_mode)), _cancel.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or System.IO.IOException or SocketException)
            {
                // The client went away, fine.
            }
        }
    }

    private static string BuildResponse(DevFakeServerMode mode)
    {
        switch (mode)
        {
            case DevFakeServerMode.BadJson:
                return Json("{ this is not json");

            case DevFakeServerMode.NullJson:
                return Json("null");

            case DevFakeServerMode.NotFound:
                return "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

            case DevFakeServerMode.Truncated:
                // Says 1000 bytes are coming and sends 9.
                return "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 1000\r\n" +
                       "Connection: close\r\n\r\n{\"build\":";

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static string Json(string body)
    {
        return "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n" +
               $"Content-Length: {Encoding.ASCII.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
    }

    public void Dispose()
    {
        _cancel.Cancel();
        _cancel.Dispose();
        _listener.Stop();
    }
}
