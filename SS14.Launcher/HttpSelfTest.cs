using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SocketsHttpHandler = System.Net.Http.SocketsHttpHandler;

namespace SS14.Launcher;

//
// HTTP self-test system.
// This does a bunch of verbose logging and testing of HttpClient.
// It's triggered if there's a HTTP error to core infra.
//
// This is to try to track down the sporadic issues some players face with TLS.
//

internal static class HttpSelfTest
{
    private static int _httpSelfTestInitiated = 0;

    private static readonly string[] SelfTestUrls =
    {
        "http://central.spacestation14.io/launcher_version.txt",
        "https://central.spacestation14.io/launcher_version.txt",
        "http://cdn.centcomm.spacestation14.com/launcher_version.txt",
        "https://cdn.centcomm.spacestation14.com/launcher_version.txt",
        "http://moon.spacestation14.com/replays/",
        "https://moon.spacestation14.com/replays/"
    };

    public static void StartSelfTest()
    {
        if (Interlocked.Increment(ref _httpSelfTestInitiated) > 1)
            return;

        Log.Error("--- INITIATING HTTP SELF-TEST ---");

        var i = 0;
        foreach (var url in SelfTestUrls)
        {
            RunTests(i, url);

            i += 1;
        }
    }

    private static async void RunTests(int id, string url)
    {
        Log.Information("SELF TEST [{Id}]: testing URL {Url}", id, url);

        await RunSingleTest("HappyEyeballsHttp", TestHappyEyeballsHttp);
        await RunSingleTest("PlainHttpClient", TestPlainHttpClient);
        await RunSingleTest("WinHttp", TestWinHttp);
        await RunSingleTest("LoggedSockets", TestLoggedSockets);

        Log.Information("SELF TEST [{Id}]: Done", id);

        return;

        async Task RunSingleTest(string name, Func<int, string, Task> test)
        {
            try
            {
                Log.Information("SELF TEST [{Id}]: {TestName}", id, name);
                await test(id, url);
                Log.Information("SELF TEST [{Id}]: {TestName} DONE", id, name);
            }
            catch (Exception e)
            {
                Log.Error(e, "SELF TEST [{Id}]: {TestName} FAILED", id, name);
            }
        }
    }

    private static async Task TestHappyEyeballsHttp(int id, string url)
    {
        using var client = HappyEyeballsHttp.CreateHttpClient(false);

        using var resp = await client.GetAsync(url);

        await VerifyResponse(id, resp);
    }

    private static async Task TestPlainHttpClient(int id, string url)
    {
        using var client = new HttpClient();

        using var resp = await client.GetAsync(url);

        await VerifyResponse(id, resp);
    }

    private static async Task TestWinHttp(int id, string url)
    {
        if (!OperatingSystem.IsWindows())
        {
            Log.Information("SELF TEST [{Id}]: Skipping because not on Windows", id);
            return;
        }

        using var client = new HttpClient(new WinHttpHandler());

        using var resp = await client.GetAsync(url);

        await VerifyResponse(id, resp);
    }

    private static async Task TestLoggedSockets(int id, string url)
    {
        var sw = new Stopwatch();

        var connectId = 0;
        using var client = new HttpClient(new SocketsHttpHandler
        {
            ConnectCallback = async (context, token) =>
            {
                var localConnectId = Interlocked.Increment(ref connectId);

                // Only test IPv4, I don't need more complexity on my plate.
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                Log.Information("SELF TEST [{Id}]: Connecting [{ConnectId}] to {EndPoint}", id, localConnectId, context.DnsEndPoint);
                await socket.ConnectAsync(context.DnsEndPoint, token).ConfigureAwait(false);
                Log.Information("SELF TEST [{Id}]: Connected [{ConnectId}] to {EndPoint}", id, localConnectId, socket.RemoteEndPoint);
                Log.Information("SELF TEST [{Id}]: Local [{ConnectId}]: {EndPoint}", id, localConnectId, socket.LocalEndPoint);

                var stream = new NetworkStream(socket, ownsSocket: true);
                return new LogDataTransferStream(
                    stream,
                    sw,
                    line => Log.Information("SELF TEST [{Id}]: HEXDUMP [{ConnectId}] {Line}", id, localConnectId, line)
                );
            }
        });

        sw.Start();
        using var resp = await client.GetAsync(url);

        await VerifyResponse(id, resp);
    }

    private static async Task VerifyResponse(int id, HttpResponseMessage response)
    {
        Log.Information("SELF TEST [{Id}]: Response status is {Status}", id, response.StatusCode);
        foreach (var (key, values) in response.Headers)
        {
            foreach (var value in values)
            {
                Log.Information("SELF TEST [{Id}]: Response header: {Header}: {Value}", id, key, value);
            }
        }

        var text = await response.Content.ReadAsStringAsync();
        Log.Information("SELF TEST [{Id}]: Response text: {Text}", id, text);

        if (response.StatusCode is < (HttpStatusCode)200 or > (HttpStatusCode)399)
            throw new Exception($"Got bad status code in response: {response.StatusCode}");
    }

    private sealed class LogDataTransferStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly Stopwatch _stopwatch;
        private readonly Action<string> _log;

        public LogDataTransferStream(Stream baseStream, Stopwatch stopwatch, Action<string> log)
        {
            _baseStream = baseStream;
            _stopwatch = stopwatch;
            _log = log;
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _baseStream.Read(buffer, offset, count);
            Log(Direction.Read, buffer.AsSpan(offset, read));
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Log(Direction.Write, buffer.AsSpan(offset, count));
            _baseStream.Write(buffer, offset, count);
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        private void Log(Direction direction, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                _log($"NOTHING {direction}");
                return;
            }

            // After cutting off the prefix log lines,
            // this format is compatible with Wireshark's "Import from hex dump".
            var elapsed = _stopwatch.Elapsed;
            var dirIndicator = direction == Direction.Read ? 'I' : 'O';
            _log($@"{dirIndicator} {elapsed:hh\:mm\:ss\.fffffff}");

            const int chunkSize = 32;
            var sb = new StringBuilder();
            for (var chunkOffset = 0; chunkOffset < data.Length; chunkOffset += chunkSize)
            {
                var remainingSize = Math.Min(chunkSize, data.Length - chunkOffset);
                var subData = data.Slice(chunkOffset, remainingSize);

                foreach (var b in subData)
                {
                    sb.Append($"{b:X2} ");
                }

                _log($"{chunkOffset:X16} {sb}");

                sb.Clear();
            }
        }

        private enum Direction
        {
            Read,
            Write
        }
    }
}
