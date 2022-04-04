using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SS14.Launcher.Utility;

public static class StreamHelper
{
    public static async Task<byte[]> ReadExactAsync(this Stream stream, int amount, CancellationToken cancel)
    {
        var data = new byte[amount];
        await ReadExactAsync(stream, data, cancel);
        return data;
    }

    public static async Task ReadExactAsync(this Stream stream, Memory<byte> into, CancellationToken cancel)
    {
        while (into.Length > 0)
        {
            var read = await stream.ReadAsync(into, cancel);

            // Check EOF.
            if (read == 0)
                throw new EndOfStreamException();

            into = into[read..];
        }
    }

    public static async Task CopyAmountToAsync(
        this Stream stream,
        Stream to,
        int amount,
        int bufferSize,
        CancellationToken cancel)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        while (amount > 0)
        {
            Memory<byte> readInto = buffer;
            if (amount < readInto.Length)
                readInto = readInto[..amount];

            var read = await stream.ReadAsync(readInto, cancel);
            if (read == 0)
                throw new EndOfStreamException();

            amount -= read;

            readInto = readInto[..read];

            await to.WriteAsync(readInto, cancel);
        }
    }
}
