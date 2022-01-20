using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Microsoft.Data.Sqlite;
using Robust.LoaderApi;
using SQLitePCL;
using static SQLitePCL.raw;

namespace SS14.Loader;

internal sealed class ContentDbFileApi : IFileApi, IDisposable
{
    private readonly Dictionary<string, (long id, int length, int compr)> _files = new();
    private readonly SemaphoreSlim _dbConnectionsSemaphore;
    private readonly ConcurrentBag<sqlite3> _dbConnections = new();
    private readonly int _connectionPoolSize;

    public ContentDbFileApi(string contentDbPath, long version)
    {
        if (sqlite3_threadsafe() == 0)
            throw new InvalidOperationException("SQLite is not thread safe!");

        var err = sqlite3_open_v2(
            contentDbPath,
            out var db,
            SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX | SQLITE_OPEN_SHAREDCACHE,
            null);
        CheckThrowSqliteErr(db, err);

        LoadManifest(version, db);

        // Create pool of connections to avoid lock contention on multithreaded scenarios.
        var poolSize = _connectionPoolSize = ConnectionPoolSize();
        _dbConnectionsSemaphore = new SemaphoreSlim(poolSize, poolSize);
        _dbConnections.Add(db);

        for (var i = 1; i < poolSize; i++)
        {
            err = sqlite3_open_v2(
                contentDbPath,
                out db,
                SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX | SQLITE_OPEN_SHAREDCACHE,
                null);
            CheckThrowSqliteErr(db, err);

            _dbConnections.Add(db);
        }
    }

    private void LoadManifest(long version, sqlite3 db)
    {
        var err = sqlite3_prepare_v2(
            db,
            @"
            SELECT c.ROWID, c.Size, c.Compression, cm.Path
            FROM Content c, ContentManifest cm
            WHERE cm.ContentId = c.Id AND cm.VersionId = ? AND cm.Path NOT LIKE '%/'",
            out var stmt);
        CheckThrowSqliteErr(db, err);

        sqlite3_bind_int64(stmt, 1, version);

        while ((err = sqlite3_step(stmt)) == SQLITE_ROW)
        {
            var rowId = sqlite3_column_int64(stmt, 0);
            var size = sqlite3_column_int(stmt, 1);
            var compression = sqlite3_column_int(stmt, 2);
            var path = sqlite3_column_text(stmt, 3).utf8_to_string();

            _files.Add(path, (rowId, size, compression));
        }
        CheckThrowSqliteErr(db, err, SQLITE_DONE);

        err = sqlite3_finalize(stmt);
        CheckThrowSqliteErr(db, err);
    }

    private static void CheckThrowSqliteErr(sqlite3 db, int err, int expect=SQLITE_OK)
    {
        if (err != expect)
            SqliteException.ThrowExceptionForRC(err, db);
    }

    private static int ConnectionPoolSize()
    {
        var envVar = Environment.GetEnvironmentVariable("SS14_LOADER_CONTENT_POOL_SIZE");
        if (!string.IsNullOrEmpty(envVar))
            return int.Parse(envVar);

        return Math.Min(2, Environment.ProcessorCount);
    }

    public void Dispose()
    {
        for (var i = 0; i < _connectionPoolSize; i++)
        {
            _dbConnectionsSemaphore.Wait();
            if (!_dbConnections.TryTake(out var db))
            {
                Console.Error.WriteLine("ERROR: Failed to retrieve content DB connection when shutting down!");
                continue;
            }

            db.Close();
        }
    }

    public bool TryOpen(string path, [NotNullWhen(true)] out Stream? stream)
    {
        if (!_files.TryGetValue(path, out var tuple))
        {
            stream = null;
            return false;
        }

        var (id, length, compression) = tuple;

        _dbConnectionsSemaphore.Wait();
        sqlite3? db = null;
        try
        {
            if (!_dbConnections.TryTake(out db))
                throw new InvalidOperationException("Entered semaphore but failed to retrieve DB connection??");

            var err = sqlite3_blob_open(db, "main", "Content", "Data", id, 0, out var blob);
            if (err != SQLITE_OK)
                SqliteException.ThrowExceptionForRC(err, db);

            if (compression == 1)
            {
                var buffer = GC.AllocateUninitializedArray<byte>(length);
                stream = new MemoryStream(buffer);

                var blobStream = new SqliteBlobStream(blob);
                using var deflater = new DeflateStream(blobStream, CompressionMode.Decompress);
                deflater.CopyTo(stream);
                stream.Position = 0;
            }
            else
            {
                using var _ = blob;

                var buffer = GC.AllocateUninitializedArray<byte>(length);
                err = sqlite3_blob_read(blob, buffer.AsSpan(), 0);
                if (err != SQLITE_OK)
                    SqliteException.ThrowExceptionForRC(err, db);

                stream = new MemoryStream(buffer, writable: false);
            }
            return true;
        }
        finally
        {
            if (db != null)
                _dbConnections.Add(db);

            _dbConnectionsSemaphore.Release();
        }
    }

    public IEnumerable<string> AllFiles => _files.Keys;

    private sealed class SqliteBlobStream : Stream
    {
        private int _pos;
        private readonly sqlite3_blob _blob;

        public SqliteBlobStream(sqlite3_blob blob)
        {
            _blob = blob;
            Length = sqlite3_blob_bytes(blob);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _blob.Close();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            var toRead = (int)Math.Min(buffer.Length, Length - _pos);
            if (toRead == 0)
                return 0;

            var err = sqlite3_blob_read(_blob, buffer[..toRead], _pos);
            if (err != SQLITE_OK)
                SqliteException.ThrowExceptionForRC(err, null);

            _pos += toRead;

            return toRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position
        {
            get => _pos;
            set => throw new NotSupportedException();
        }
    }
}
