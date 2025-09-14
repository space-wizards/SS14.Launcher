using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Serilog;
using SpaceWizards.Sodium;
using SQLitePCL;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models;

//
// Logic for updater manifest (delta) downloads.
//

public sealed partial class Updater
{
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverloadWithCancellation")]
    [SuppressMessage("ReSharper", "UseAwaitUsing")]
    private async Task<byte[]> ManifestDownloadNewVersion(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        long versionId,
        TransactedDownloadState state,
        CancellationToken cancel)
    {
        var swZstd = new Stopwatch();
        var swSqlite = new Stopwatch();
        var swBlake = new Stopwatch();

        // Download manifest first.

        Status = UpdateStatus.FetchingClientManifest;

        var fetchedManifest = await ManifestFetchContentManifest(buildInfo, cancel);
        var toDownload = ManifestCalculateFilesToDownload(fetchedManifest, con, swSqlite);

        Progress = null;
        Status = UpdateStatus.DownloadingClientUpdate;

        if (toDownload.Count > 0)
        {
            // Have missing files, need to download them.

            Log.Debug(
                "Missing {MissingContentBlobs} blobs, downloading from {ManifestDownloadUrl}",
                toDownload.Count,
                buildInfo.ManifestDownloadUrl!);

            await ManifestDownloadMissingContent(
                buildInfo,
                con,
                fetchedManifest,
                toDownload,
                state,
                swSqlite,
                swZstd,
                swBlake,
                cancel);
        }

        Log.Debug("ZSTD: {ZStdElapsed} ms | SQLite: {SqliteElapsed} ms | Blake2B: {Blake2BElapsed} ms",
            swZstd.ElapsedMilliseconds,
            swSqlite.ElapsedMilliseconds,
            swBlake.ElapsedMilliseconds);

        ManifestFillContentManifest(con, versionId, fetchedManifest);

#if DEBUG
        var testHash = GenerateContentManifestHash(con, versionId);
        Debug.Assert(testHash.AsSpan().SequenceEqual(fetchedManifest.ManifestHash));
#endif

        return fetchedManifest.ManifestHash;
    }

    private async Task<FetchedContentManifestData> ManifestFetchContentManifest(
        ServerBuildInformation buildInfo,
        CancellationToken cancel)
    {
        Log.Debug("Downloading content manifest from {ContentManifestUrl}", buildInfo.ManifestUrl);

        var request = new HttpRequestMessage(HttpMethod.Get, buildInfo.ManifestUrl);
        var manifestResp = await _http.SendZStdAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
        manifestResp.EnsureSuccessStatusCode();

        var manifest = Blake2BHasherStream.CreateReader(
            await manifestResp.Content.ReadAsStreamAsync(cancel),
            ReadOnlySpan<byte>.Empty,
            32);

        // Go over the manifest, reading it into the SQLite ContentManifest table.
        // For any content blobs we don't have yet, we put a placeholder entry in the database for now.
        // Keep track of all files we need to download for later.

        using var sr = new StreamReader(manifest);

        if (await sr.ReadLineAsync(cancel) != "Robust Content Manifest 1")
            throw new UpdateException("Unknown manifest header!");

        Log.Debug("Parsing manifest...");

        var entries = new List<ContentManifestEntry>();

        while (await sr.ReadLineAsync(cancel) is { } manifestLine)
        {
            var sep = manifestLine.IndexOf(' ');
            var hash = Convert.FromHexString(manifestLine.AsSpan(0, sep));
            var filename = manifestLine.AsMemory(sep + 1);

            entries.Add(new ContentManifestEntry
            {
                Hash = hash,
                Path = filename.ToString(),
            });
        }

        Log.Debug("Total of {ManifestEntriesCount} manifest entries", entries.Count);

        var manifestHash = manifest.Finish();
        if (Convert.ToHexString(manifestHash) != buildInfo.ManifestHash)
            throw new UpdateException("Manifest has incorrect hash!");

        Log.Debug("Successfully validated manifest hash");

        return new FetchedContentManifestData
        {
            ManifestHash = manifestHash,
            Entries = entries,
        };
    }

    private static List<int> ManifestCalculateFilesToDownload(
        FetchedContentManifestData manifestData,
        SqliteConnection con,
        Stopwatch swSqlite)
    {
        Debug.Assert(con.Handle != null);
        var db = con.Handle;

        swSqlite.Start();

        var toDownload = new List<int>();
        var queuedHashes = new HashSet<HashKey>();

        using var stmtFindContentRow = db.Prepare("SELECT Id FROM Content WHERE Hash = ?");

        for (var i = 0; i < manifestData.Entries.Count; i++)
        {
            var entry = manifestData.Entries[i];
            var key = new HashKey(entry.Hash);

            if (queuedHashes.Contains(key))
                continue;

            stmtFindContentRow.BindBlob(db, 1, entry.Hash);
            var stepResult = stmtFindContentRow.Step(db);

            stmtFindContentRow.Reset(db);

            if (stepResult == raw.SQLITE_DONE)
            {
                // Does not exist in DB. We need to download it.
                toDownload.Add(i);
                // A blob can appear multiple times in the manifest. Avoid downloading it twice.
                queuedHashes.Add(key);
            }
        }

        swSqlite.Stop();

        return toDownload;
    }

    private async Task ManifestDownloadMissingContent(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        FetchedContentManifestData manifestData,
        List<int> toDownload,
        TransactedDownloadState state,
        Stopwatch swSqlite,
        Stopwatch swZstd,
        Stopwatch swBlake,
        CancellationToken cancel)
    {
        await CheckManifestDownloadServerProtocolVersions(buildInfo.ManifestDownloadUrl!, cancel);

        // Alright well we support the protocol. Now to start the HTTP request!


        // Write request body.
        var requestBody = new byte[toDownload.Count * 4];
        var reqI = 0;
        foreach (var idx in toDownload)
        {
            BinaryPrimitives.WriteInt32LittleEndian(requestBody.AsSpan(reqI, 4), idx);
            reqI += 4;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, buildInfo.ManifestDownloadUrl);
        request.Headers.Add(
            "X-Robust-Download-Protocol",
            ManifestDownloadProtocolVersion.ToString(CultureInfo.InvariantCulture));

        request.Content = new ByteArrayContent(requestBody);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        Log.Debug("Starting download...");

        Status = UpdateStatus.DownloadingClientUpdate;

        // Send HTTP request

        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("zstd"));
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancel);
        var bandwidthStream = new BandwidthStream(stream);
        stream = bandwidthStream;
        if (response.Content.Headers.ContentEncoding.Contains("zstd"))
        {
            Log.Debug("Stream compression is active");
            stream = new ZStdDecompressStream(stream);
        }

        await using var streamDispose = stream;

        // Read flags header
        var streamHeader = await stream.ReadExactAsync(4, cancel);
        var streamFlags = (DownloadStreamHeaderFlags)BinaryPrimitives.ReadInt32LittleEndian(streamHeader);
        var preCompressed = (streamFlags & DownloadStreamHeaderFlags.PreCompressed) != 0;

        // compressContext.SetParameter(ZSTD_cParameter.ZSTD_c_nbWorkers, 4);
        // If the stream is pre-compressed we need to decompress the blobs to verify BLAKE2B hash.
        // If it isn't, we need to manually try re-compressing individual files to store them.
        var compressContext = preCompressed ? null : new ZStdCCtx();
        var decompressContext = preCompressed ? new ZStdDCtx() : null;

        // Normal file header:
        // <int32> uncompressed length
        // When preCompressed is set, we add:
        // <int32> compressed length
        var fileHeader = new byte[preCompressed ? 8 : 4];

        var db = con.Handle;
        Debug.Assert(db != null);

        SqliteBlobStream? blob = null;
        try
        {
            using var stmtInsertContent = db.Prepare("""
                INSERT INTO Content (Hash, Size, Compression, Data)
                VALUES (@Hash, @Size, @Compression, zeroblob(@DataSize))
                RETURNING Id
                """);

            // Buffer for storing compressed ZStd data.
            var compressBuffer = new byte[1024];

            // Buffer for storing uncompressed data.
            var readBuffer = new byte[1024];

            var hash = new byte[256 / 8];

            for (var i = 0; i < toDownload.Count; i++)
            {
                // Simple loop stuff.
                cancel.ThrowIfCancellationRequested();

                var manifestEntry = manifestData.Entries[toDownload[i]];

                Progress = (i, toDownload.Count, ProgressUnit.None);
                Speed = bandwidthStream.CalcCurrentAvg();

                // Read file header.
                await stream.ReadExactAsync(fileHeader, cancel);

                var length = BinaryPrimitives.ReadInt32LittleEndian(fileHeader.AsSpan(0, 4));

                EnsureBuffer(ref readBuffer, length);
                var data = readBuffer.AsMemory(0, length);

                // Data to write to database.
                var compression = ContentCompressionScheme.None;
                var writeData = data;

                if (preCompressed)
                {
                    // Compressed length from extended header.
                    var compressedLength = BinaryPrimitives.ReadInt32LittleEndian(fileHeader.AsSpan(4, 4));

                    // Log.Debug("{index:D5}: {blobLength:D8} {dataLength:D8}", idx, length, compressedLength);

                    if (compressedLength > 0)
                    {
                        EnsureBuffer(ref compressBuffer, compressedLength);
                        var compressedData = compressBuffer.AsMemory(0, compressedLength);
                        await stream.ReadExactAsync(compressedData, cancel);

                        // Decompress so that we can verify hash down below.
                        // TODO: It's possible to hash while we're decompressing to avoid using a full buffer.

                        swZstd.Start();
                        var decompressedLength = decompressContext!.Decompress(data.Span, compressedData.Span);
                        swZstd.Stop();

                        if (decompressedLength != data.Length)
                            throw new UpdateException($"Compressed blob {i} had incorrect decompressed size!");

                        // Set variables so that the database write down below uses them.
                        compression = ContentCompressionScheme.ZStd;
                        writeData = compressedData;
                    }
                    else
                    {
                        await stream.ReadExactAsync(data, cancel);
                    }
                }
                else
                {
                    await stream.ReadExactAsync(data, cancel);
                }

                swBlake.Start();
                CryptoGenericHashBlake2B.Hash(hash, data.Span, ReadOnlySpan<byte>.Empty);
                swBlake.Stop();

                /*
                Log.Verbose(
                    "[{Index}] {FileName}: {Size} ({Hash})",
                    toDownload[i],
                    manifestEntry.Path,
                    data.Span.Length,
                    Convert.ToHexString(hash));
                */

                if (!manifestEntry.Hash.AsSpan().SequenceEqual(hash))
                    throw new UpdateException("Hash mismatch while downloading!");

                if (!preCompressed)
                {
                    // File wasn't pre-compressed. We should try to manually compress it to save space in DB.

                    swZstd.Start();

                    EnsureBuffer(ref compressBuffer, ZStd.CompressBound(data.Length));
                    var compressLength = compressContext!.Compress(compressBuffer, data.Span);

                    swZstd.Stop();

                    // Don't bother saving compressed data if it didn't save enough space.
                    if (compressLength + CompressionSavingsThreshold < length)
                    {
                        // Set variables so that the database write down below uses them.
                        compression = ContentCompressionScheme.ZStd;
                        writeData = compressBuffer.AsMemory(0, compressLength);
                    }
                }

                swSqlite.Start();

                stmtInsertContent.BindBlob(db, 1, manifestEntry.Hash); // @Hash
                stmtInsertContent.BindInt(db, 2, length); // @Size
                stmtInsertContent.BindInt(db, 3, (int)compression); // @Compression
                stmtInsertContent.BindInt(db, 4, writeData.Length); // @DataSize

                stmtInsertContent.Step(db);

                var rowId = raw.sqlite3_column_int64(stmtInsertContent, 0);

                stmtInsertContent.Reset(db);

                if (blob == null)
                    blob = SqliteBlobStream.Open(con.Handle!, "main", "Content", "Data", rowId, true);
                else
                    blob.Reopen(rowId);

                blob.Write(writeData.Span);
                swSqlite.Stop();

                state.DownloadedContentEntries.Add(rowId);

                // Log.Debug("Data size: {DataSize}, Size: {UncompressedLen}", writeData.Length, uncompressedLen);
            }
        }
        finally
        {
            blob?.Dispose();
            decompressContext?.Dispose();
            compressContext?.Dispose();
        }

        Progress = null;
        Speed = null;
    }

    private async Task CheckManifestDownloadServerProtocolVersions(string url, CancellationToken cancel)
    {
        // Check that we support the required protocol versions for the download server.

        Log.Debug("Checking supported protocols on download server...");

        // Do HTTP OPTIONS to figure out supported download protocol versions.
        var request = new HttpRequestMessage(HttpMethod.Options, url);

        var resp = await _http.SendAsync(request, cancel);
        resp.EnsureSuccessStatusCode();

        if (!resp.Headers.TryGetValues("X-Robust-Download-Min-Protocol", out var minHeaders)
            || !resp.Headers.TryGetValues("X-Robust-Download-Max-Protocol", out var maxHeaders))
        {
            throw new UpdateException("Missing required headers from OPTIONS on manifest download URL!");
        }

        if (!int.TryParse(minHeaders.First(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var min)
            || !int.TryParse(maxHeaders.First(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var max))
        {
            throw new UpdateException("Invalid version headers on OPTIONS on manifest download URL!");
        }

        Log.Debug("Download server protocol min: {MinProtocolVersion} max: {MaxProtocolVersion}", min, max);

        if (min > ManifestDownloadProtocolVersion || max < ManifestDownloadProtocolVersion)
        {
            throw new UpdateException("No supported protocol version for download server.");
        }
    }

    private static void ManifestFillContentManifest(
        SqliteConnection connection,
        long versionId,
        FetchedContentManifestData manifestData)
    {
        var db = connection.Handle;
        Debug.Assert(db != null);

        using var stmtFindContent = db.Prepare("SELECT Id FROM Content WHERE Hash = ?");
        using var stmtInsertContentManifest =
            db.Prepare("INSERT INTO ContentManifest (VersionId, Path, ContentId) VALUES (?, ?, ?)");

        stmtInsertContentManifest.BindInt64(db, 1, versionId);

        foreach (var entry in manifestData.Entries)
        {
            stmtFindContent.BindBlob(db, 1, entry.Hash);

            var result = stmtFindContent.Step(db);
            if (result == raw.SQLITE_DONE)
            {
                // Shouldn't be possible, we should have all blobs we need!
                throw new UnreachableException("Missing content blob during manifest fill!");
            }

            var contentId = raw.sqlite3_column_int64(stmtFindContent, 0);
            stmtFindContent.Reset(db);

            stmtInsertContentManifest.BindString(db, 2, entry.Path);
            stmtInsertContentManifest.BindInt64(db, 3, contentId);
            stmtInsertContentManifest.Step(db);
            stmtInsertContentManifest.Reset(db);
        }
    }

    [Flags]
    public enum DownloadStreamHeaderFlags
    {
        None = 0,

        /// <summary>
        /// If this flag is set on the download stream, individual files have been pre-compressed by the server.
        /// This means each file has a compression header, and the launcher should not attempt to compress files itself.
        /// </summary>
        PreCompressed = 1 << 0
    }

    private sealed class FetchedContentManifestData
    {
        public required byte[] ManifestHash;
        public required List<ContentManifestEntry> Entries;
    }

    private struct ContentManifestEntry
    {
        public required string Path;
        public required byte[] Hash;
    }
}
