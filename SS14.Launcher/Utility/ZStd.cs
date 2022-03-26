﻿using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SharpZstd.Interop;
using static SharpZstd.Interop.Zstd;

namespace SS14.Launcher.Utility;

[Serializable]
public class ZStdException : Exception
{
    public ZStdException()
    {
    }

    public ZStdException(string message) : base(message)
    {
    }

    public ZStdException(string message, Exception inner) : base(message, inner)
    {
    }

    protected ZStdException(
        SerializationInfo info,
        StreamingContext context) : base(info, context)
    {
    }

    public static unsafe ZStdException FromCode(nuint code)
    {
        return new ZStdException(Marshal.PtrToStringUTF8((IntPtr)ZSTD_getErrorName(code))!);
    }

    public static void ThrowIfError(nuint code)
    {
        if (ZSTD_isError(code) != 0)
            throw FromCode(code);
    }
}

public sealed class ZStdDecompressStream : Stream
{
    private readonly Stream _baseStream;
    private readonly bool _ownStream;
    private readonly unsafe ZSTD_DCtx* _ctx;
    private readonly byte[] _buffer;
    private int _bufferPos;
    private int _bufferSize;
    private bool _disposed;

    public unsafe ZStdDecompressStream(Stream baseStream, bool ownStream = true)
    {
        _baseStream = baseStream;
        _ownStream = ownStream;
        _ctx = ZSTD_createDCtx();
        _buffer = ArrayPool<byte>.Shared.Rent((int)ZSTD_DStreamInSize());
    }

    protected override unsafe void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;
        ZSTD_freeDCtx(_ctx);

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
                var outputBuf = new ZSTD_outBuffer { dst = outputPtr, pos = 0, size = (nuint)buffer.Length };
                var inputBuf = new ZSTD_inBuffer { src =  inputPtr, pos = (nuint)_bufferPos, size = (nuint)_bufferSize};
                var ret = ZSTD_decompressStream(_ctx, &outputBuf, &inputBuf);

                _bufferPos = (int)inputBuf.pos;
                ZStdException.ThrowIfError(ret);

                if (outputBuf.pos > 0)
                    return (int)outputBuf.pos;
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
                    ZSTD_outBuffer outputBuf = default;
                    outputBuf.dst = outputPtr;
                    outputBuf.pos = 0;
                    outputBuf.size = (nuint)buffer.Length;
                    ZSTD_inBuffer inputBuf = default;
                    inputBuf.src = inputPtr;
                    inputBuf.pos = (nuint)_bufferPos;
                    inputBuf.size = (nuint)_bufferSize;

                    var ret = ZSTD_decompressStream(_ctx, &outputBuf, &inputBuf);

                    _bufferPos = (int)inputBuf.pos;
                    ZStdException.ThrowIfError(ret);

                    if (outputBuf.pos > 0)
                        return (int)outputBuf.pos;
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

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ZStdDecompressStream));
    }
}

public sealed class ZStdCompressStream : Stream
{
    private readonly Stream _baseStream;
    private readonly bool _ownStream;
    private readonly unsafe ZSTD_CCtx* _ctx;
    private readonly byte[] _buffer;
    private int _bufferPos;
    private bool _disposed;

    public unsafe ZStdCompressStream(Stream baseStream, bool ownStream = true)
    {
        _ctx = ZSTD_createCCtx();
        _baseStream = baseStream;
        _ownStream = ownStream;
        _buffer = ArrayPool<byte>.Shared.Rent((int)ZSTD_CStreamOutSize());
    }

    public override void Flush()
    {
        FlushInternal(ZSTD_EndDirective.ZSTD_e_flush);
    }

    public void FlushEnd()
    {
        FlushInternal(ZSTD_EndDirective.ZSTD_e_end);
    }

    private unsafe void FlushInternal(ZSTD_EndDirective directive)
    {
        fixed (byte* outPtr = _buffer)
        {
            ZSTD_outBuffer outBuf = default;
            outBuf.size = (nuint)_buffer.Length;
            outBuf.pos = (nuint)_bufferPos;
            outBuf.dst = outPtr;

            ZSTD_inBuffer inBuf;

            while (true)
            {
                var err = ZSTD_compressStream2(_ctx, &outBuf, &inBuf, directive);
                ZStdException.ThrowIfError(err);
                _bufferPos = (int)outBuf.pos;

                _baseStream.Write(_buffer.AsSpan(0, (int)outBuf.pos));
                _bufferPos = 0;
                outBuf.pos = 0;

                if (err == 0)
                    break;
            }
        }

        _baseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
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
        Write(buffer.AsSpan(offset, count));
    }

    public override unsafe void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfDisposed();

        fixed (byte* outPtr = _buffer)
        fixed (byte* inPtr = buffer)
        {
            ZSTD_outBuffer outBuf = default;
            outBuf.size = (nuint)_buffer.Length;
            outBuf.pos = (nuint)_bufferPos;
            outBuf.dst = outPtr;

            ZSTD_inBuffer inBuf = default;
            inBuf.pos = 0;
            inBuf.size = (nuint)buffer.Length;
            inBuf.src = inPtr;

            while (true)
            {
                var err = ZSTD_compressStream2(_ctx, &outBuf, &inBuf, ZSTD_EndDirective.ZSTD_e_continue);
                ZStdException.ThrowIfError(err);
                _bufferPos = (int)outBuf.pos;

                if (inBuf.pos >= inBuf.size)
                    break;

                // Not all input data consumed. Flush output buffer and continue.
                _baseStream.Write(_buffer.AsSpan(0, (int)outBuf.pos));
                _bufferPos = 0;
                outBuf.pos = 0;
            }
        }
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    protected override unsafe void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_disposed)
            return;

        _disposed = true;
        ZSTD_freeCCtx(_ctx);

        if (disposing)
        {
            if (_ownStream)
                _baseStream.Dispose();

            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ZStdCompressStream));
    }
}
