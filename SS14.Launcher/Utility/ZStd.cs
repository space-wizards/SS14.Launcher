using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImpromptuNinjas.ZStd;
using Buffer = ImpromptuNinjas.ZStd.Buffer;

namespace SS14.Launcher.Utility;

internal static class ZStdConstants
{
    public const int ZSTD_BLOCKHEADERSIZE = 3;

    public const int ZSTD_BLOCKSIZE_MAX = 1 << 17;

    // ZSTD_DStreamInSize (not exposed by ImpromptuNinjas.ZStd, this is from the C code)
    public const int ZSTD_DSTREAMIN_SIZE = ZSTD_BLOCKSIZE_MAX + ZSTD_BLOCKHEADERSIZE;
}


// ZStdDecompressStream in ImpromptuNinjas.ZStd is broken.
// Make our own.
public sealed class ZStdDecompressStream : Stream
{
    private readonly Stream _baseStream;
    private readonly bool _ownStream;
    private readonly unsafe DCtx* _ctx;
    private readonly byte[] _buffer;
    private int _bufferPos;
    private int _bufferSize;
    private bool _disposed;

    public unsafe ZStdDecompressStream(Stream baseStream, bool ownStream = true)
    {
        _baseStream = baseStream;
        _ownStream = ownStream;
        _ctx = Native.ZStdDCtx.CreateDCtx();
        _buffer = ArrayPool<byte>.Shared.Rent(ZStdConstants.ZSTD_DSTREAMIN_SIZE);
    }

    protected override unsafe void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;
        Native.ZStdDCtx.FreeDCtx(_ctx);

        if (disposing)
        {
            if (_ownStream)
                _baseStream.Dispose();

            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }

    public override void Flush()
    {
        ThrowIfDisposed();
        _baseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int ReadByte()
    {
        Span<byte> buf = stackalloc byte[1];
        return Read(buf) == 0 ? -1 : buf[0];
    }

    public override unsafe int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        do
        {
            if (_bufferSize == 0 || _bufferPos == _bufferSize)
            {
                _bufferPos = 0;
                _bufferSize = _baseStream.Read(_buffer);

                if (_bufferSize == 0)
                    return 0;
            }

            fixed (byte* inputPtr = _buffer)
            fixed (byte* outputPtr = buffer)
            {
                var outputBuffer = new Buffer(outputPtr, (UIntPtr)buffer.Length, (UIntPtr)0);
                var inputBuffer = new Buffer(inputPtr, (UIntPtr)_bufferSize, (UIntPtr)_bufferPos);
                var ret = Native.ZStdDCtx.StreamDecompress(_ctx, ref outputBuffer, ref inputBuffer);

                _bufferPos = (int)inputBuffer.Position;
                ThrowIfError(ret);

                if ((nuint)outputBuffer.Position > 0)
                    return (int)outputBuffer.Position;
            }
        } while (true);
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        do
        {
            if (_bufferSize == 0 || _bufferPos == _bufferSize)
            {
                _bufferPos = 0;
                _bufferSize = await _baseStream.ReadAsync(_buffer, cancellationToken);

                if (_bufferSize == 0)
                    return 0;
            }

            unsafe
            {
                fixed (byte* inputPtr = _buffer)
                fixed (byte* outputPtr = buffer.Span)
                {
                    var outputBuffer = new Buffer(outputPtr, (UIntPtr)buffer.Length, (UIntPtr)0);
                    var inputBuffer = new Buffer(inputPtr, (UIntPtr)_bufferSize, (UIntPtr)_bufferPos);
                    var ret = Native.ZStdDCtx.StreamDecompress(_ctx, ref outputBuffer, ref inputBuffer);

                    ThrowIfError(ret);

                    if ((nuint)outputBuffer.Position > 0)
                        return (int)outputBuffer.Position;
                }
            }
        } while (true);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private static void ThrowIfError(UIntPtr code)
    {
        if (Native.ZStd.IsError(code) != 0)
            throw new ZStdException(Native.ZStd.GetErrorName(code));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ZStdDecompressStream));
    }
}
