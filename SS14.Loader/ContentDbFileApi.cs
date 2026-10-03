using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Microsoft.Data.Sqlite;
using Robust.LoaderApi;
using SharpZstd.Interop;
using SQLitePCL;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Utility;
using static SQLitePCL.raw;
using static SharpZstd.Interop.Zstd;

namespace SS14.Loader;

internal sealed class ContentDbFileApi : IFileApi, IDisposable
{
    private readonly Dictionary<string, (long id, int length, ContentCompressionScheme compr)> _files = new();
    private readonly SemaphoreSlim _dbConnectionsSemaphore;
    private readonly ConcurrentBag<ConPoolEntry> _dbConnections = new();
    private readonly int _connectionPoolSize;

    public unsafe ContentDbFileApi(string contentDbPath, long version)
    {
        if (sqlite3_threadsafe() == 0)
            throw new InvalidOperationException("SQLite is not thread safe!");

        var err = sqlite3_open_v2(
            contentDbPath,
            out var db,
            SQLITE_OPEN_READWRITE | SQLITE_OPEN_NOMUTEX,
            null);

        AddRunningClient(db, version);

        // Make sure to have a read transaction on every database connection
        // so that the launcher can't delete anything from underneath us if the user does anything.
        sqlite3_exec(db, "BEGIN");

        CheckThrowSqliteErr(db, err);

        LoadManifest(version, db, out var initBlob);

        // Create pool of connections to avoid lock contention on multithreaded scenarios.
        var poolSize = _connectionPoolSize = ConnectionPoolSize();
        _dbConnectionsSemaphore = new SemaphoreSlim(poolSize, poolSize);
        _dbConnections.Add(new ConPoolEntry(db, ZSTD_createDCtx(), InitBlob()) { IsMaster = true });

        for (var i = 1; i < poolSize; i++)
        {
            err = sqlite3_open_v2(
                contentDbPath,
                out db,
                SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX,
                null);
            CheckThrowSqliteErr(db, err);

            sqlite3_exec(db, "BEGIN");

            _dbConnections.Add(new ConPoolEntry(db, ZSTD_createDCtx(), InitBlob()));
        }

        sqlite3_blob InitBlob()
        {
            var rc = sqlite3_blob_open(db, "main", "Content", "Data", initBlob, 0, out var blob);
            SqliteException.ThrowExceptionForRC(rc, db);
            return blob;
        }
    }

    private static void AddRunningClient(sqlite3 db, long contentVersion)
    {
        var err = sqlite3_prepare_v2(
            db,
            """
            INSERT OR REPLACE INTO
                RunningClient (ProcessId, MainModule, UsedVersion)
            VALUES
                (?, ?, ?)
            """,
            out var stmt);
        CheckThrowSqliteErr(db, err);
        sqlite3_bind_int(stmt, 1, Environment.ProcessId);
        sqlite3_bind_text(stmt, 2, Process.GetCurrentProcess().MainModule?.FileName ?? "");
        sqlite3_bind_int64(stmt, 3, contentVersion);

        sqlite3_step(stmt);

        stmt.Dispose();
    }

    private static void ClearRunningClient(sqlite3 db)
    {
        // Ensure we are not in a transaction anymore.
        sqlite3_exec(db, "ROLLBACK");

        var err = sqlite3_prepare_v2(
            db,
            """
            DELETE FROM RunningClient
            WHERE ProcessId = ?
            """,
            out var stmt);
        CheckThrowSqliteErr(db, err);
        sqlite3_bind_int(stmt, 1, Environment.ProcessId);

        sqlite3_step(stmt);

        stmt.Dispose();
    }

    private void LoadManifest(long version, sqlite3 db, out long initBlob)
    {
        initBlob = default;

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
            var compression = (ContentCompressionScheme) sqlite3_column_int(stmt, 2);
            var path = sqlite3_column_text(stmt, 3).utf8_to_string();

            _files.Add(path, (rowId, size, compression));

            initBlob = rowId;
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

        return Math.Max(2, Environment.ProcessorCount);
    }

    public void Dispose()
    {
        sqlite3? master = null;
        for (var i = 0; i < _connectionPoolSize; i++)
        {
            _dbConnectionsSemaphore.Wait();
            if (!_dbConnections.TryTake(out var db))
            {
                Console.Error.WriteLine("ERROR: Failed to retrieve content DB connection when shutting down!");
                continue;
            }

            if (db.IsMaster)
                master = db.Connection;
            else
                db.Connection.Close();

            db.Blob.Close();
        }

        Debug.Assert(master != null);

        ClearRunningClient(master);

        master.Dispose();
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
        ConPoolEntry? entry = null;
        try
        {
            if (!_dbConnections.TryTake(out entry))
                throw new InvalidOperationException("Entered semaphore but failed to retrieve DB connection??");

            var db = entry.Connection;
            var blob = entry.Blob;

            var err = sqlite3_blob_reopen(blob, id);
            if (err != SQLITE_OK)
                SqliteException.ThrowExceptionForRC(err, db);

            switch (compression)
            {
                case ContentCompressionScheme.Deflate:
                {
                    var buffer = GC.AllocateUninitializedArray<byte>(length);
                    stream = new MemoryStream(buffer);

                    using var blobStream = new SqliteBlobStream(blob, canWrite: false, ownsBlob: false);
                    using var deflater = new DeflateStream(blobStream, CompressionMode.Decompress);
                    deflater.CopyTo(stream);
                    stream.Position = 0;
                    break;
                }
                case ContentCompressionScheme.ZStd:
                {
                    var buffer = GC.AllocateUninitializedArray<byte>(length);
                    stream = new MemoryStream(buffer, writable: false);

                    unsafe
                    {
                        ReadBlobZStd(buffer, blob, db, entry.DecompressionContext);
                    }
                    break;
                }
                case ContentCompressionScheme.None:
                {
                    var buffer = GC.AllocateUninitializedArray<byte>(length);
                    err = sqlite3_blob_read(blob, buffer.AsSpan(), 0);
                    if (err != SQLITE_OK)
                        SqliteException.ThrowExceptionForRC(err, db);

                    stream = new MemoryStream(buffer, writable: false);
                    break;
                }
                default:
                    throw new NotSupportedException($"Unknown compression scheme: {compression}");
            }
            return true;
        }
        finally
        {
            if (entry != null)
                _dbConnections.Add(entry);

            _dbConnectionsSemaphore.Release();
        }
    }

    private static unsafe void ReadBlobZStd(Span<byte> into, sqlite3_blob blob, sqlite3 db, ZSTD_DCtx* context)
    {
        var remainingInput = sqlite3_blob_bytes(blob);
        var blobOffset = 0;
        var buffer = ArrayPool<byte>.Shared.Rent((int)ZSTD_DStreamInSize());
        try
        {
            while (true)
            {
                var toRead = Math.Min(buffer.Length, remainingInput);
                var rc = sqlite3_blob_read(blob, buffer.AsSpan(0, toRead), blobOffset);
                SqliteException.ThrowExceptionForRC(rc, db);
                blobOffset += toRead;
                remainingInput -= toRead;

                fixed (byte* inputPtr = buffer)
                fixed (byte* outputPtr = into)
                {
                    var inputBuf = new ZSTD_inBuffer { src =  inputPtr, pos = 0, size = (nuint)toRead};
                    var outputBuf = new ZSTD_outBuffer { dst = outputPtr, pos = 0, size = (nuint)into.Length };

                    var err = ZSTD_decompressStream(context, &outputBuf, &inputBuf);
                    ZStdException.ThrowIfError(err);

                    into = into[(int)outputBuf.pos..];

                    // We know the output buffer always has enough space so if this returns > 0 then we need more input.
                    if (err == (UIntPtr)0)
                    {
                        if (into.Length != 0)
                        {
                            // Safety check to avoid leaking any uninitialized data if I just screwed up.
                            throw new InvalidOperationException("Failed to fill buffer!");
                        }
                        return;
                    }
                }
            }
        }
        finally
        {
            ZSTD_DCtx_reset(context, ZSTD_ResetDirective.ZSTD_reset_session_only);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed unsafe class ConPoolEntry
    {
        public readonly sqlite3 Connection;
        public readonly ZSTD_DCtx* DecompressionContext;
        public readonly sqlite3_blob Blob;
        public bool IsMaster;

        public ConPoolEntry(sqlite3 connection, ZSTD_DCtx* decompressionContext, sqlite3_blob blob)
        {
            Connection = connection;
            DecompressionContext = decompressionContext;
            Blob = blob;
        }
    }

    public IEnumerable<string> AllFiles => _files.Keys;
}
