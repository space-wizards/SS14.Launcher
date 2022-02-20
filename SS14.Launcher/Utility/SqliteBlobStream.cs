using System;
using System.IO;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using static SQLitePCL.raw;

namespace SS14.Launcher.Utility;

/// <summary>
/// Expecting Microsoft top engineers to understand basic API design principles is too much to ask for,
/// so I can't in any way use blob re-open with Microsoft.Data.Sqlite.SqliteBlob.
/// </summary>
internal sealed class SqliteBlobStream : Stream
{
    private readonly sqlite3_blob _blob;
    private readonly bool _ownsBlob;
    private int _length;
    private int _pos;
    private bool _disposed;

    public SqliteBlobStream(sqlite3_blob blob, bool canWrite, bool ownsBlob)
    {
        _blob = blob;
        _ownsBlob = ownsBlob;
        CanWrite = canWrite;
        _length = sqlite3_blob_bytes(blob);
    }

    public static SqliteBlobStream Open(
        sqlite3 con,
        string db,
        string table,
        string column,
        long rowId,
        bool canWrite)
    {
        var rc = sqlite3_blob_open(con, db, table, column, rowId, canWrite ? 1 : 0, out var blob);
        if (rc != SQLITE_OK)
        {
            blob.Dispose();
            SqliteException.ThrowExceptionForRC(rc, con);
        }

        return new SqliteBlobStream(blob, canWrite, true);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_ownsBlob)
            _blob.Dispose();
    }

    public void Reopen(long newRodId)
    {
        ThrowIfDisposed();

        var rc = sqlite3_blob_reopen(_blob, newRodId);
        SqliteException.ThrowExceptionForRC(rc, null);

        _length = sqlite3_blob_bytes(_blob);
        _pos = 0;
    }

    public override void Flush()
    {
        // Nada.
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int ReadByte()
    {
        Span<byte> buf = stackalloc byte[1];
        return Read(buf) == 0 ? -1 : buf[0];
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();

        var toRead = (int)Math.Min(buffer.Length, Length - _pos);
        if (toRead == 0)
            return 0;

        var err = sqlite3_blob_read(_blob, buffer[..toRead], _pos);
        SqliteException.ThrowExceptionForRC(err, null);

        _pos += toRead;

        return toRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();

        switch (origin)
        {
            case SeekOrigin.Begin:
                Position = offset;
                break;
            case SeekOrigin.Current:
                Position += offset;
                break;
            case SeekOrigin.End:
                Position = Length - offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        return Position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void WriteByte(byte value)
    {
        Span<byte> buffer = stackalloc byte[1] { value };
        Write(buffer);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length + _pos > _length)
            throw new InvalidOperationException("Not enough space for write");

        var rc = sqlite3_blob_write(_blob, buffer, _pos);
        SqliteException.ThrowExceptionForRC(rc, null);

        _pos += buffer.Length;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite { get; }

    public override long Length => _length;

    public override long Position
    {
        get => _pos;
        set
        {
            ThrowIfDisposed();

            var cast = (int)value;
            if (cast < 0 || cast > _pos)
                throw new ArgumentOutOfRangeException(nameof(value));

            _pos = cast;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqliteBlobStream));
    }
}
