using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using SharpZstd.Interop;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models;

//
// Logic for updater zip downloads.
// Mostly legacy now, but keeping support is good.
//

public sealed partial class Updater
{
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverloadWithCancellation")]
    [SuppressMessage("ReSharper", "UseAwaitUsing")]
    private async Task<byte[]> ZipDownloadNewVersion(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        long versionId,
        CancellationToken cancel)
    {
        // Temp file to download zip into.
        await using var tempFile = TempFile.CreateTempFile();

        var zipHash = await ZipUpdateDownloadContent(tempFile, buildInfo, cancel);

        con.Execute("UPDATE ContentVersion SET ZipHash=@ZipHash WHERE Id=@Version",
            new { ZipHash = zipHash, Version = versionId });

        Status = UpdateStatus.LoadingIntoDb;

        tempFile.Seek(0, SeekOrigin.Begin);

        // File downloaded, time to dump this into the DB.

        var zip = new ZipArchive(tempFile, ZipArchiveMode.Read, leaveOpen: true);

        ZipIngest(con, versionId, zip, false, cancel);

        return GenerateContentManifestHash(con, versionId);
    }

    /// <summary>
    /// Download content zip to the specified file and verify hash.
    /// </summary>
    /// <returns>
    /// File hash in case the server didn't provide one.
    /// </returns>
    private async Task<byte[]> ZipUpdateDownloadContent(
        Stream file,
        ServerBuildInformation buildInformation,
        CancellationToken cancel)
    {
        Status = UpdateStatus.DownloadingClientUpdate;

        Log.Information("Downloading content update from {ContentDownloadUrl}", buildInformation.DownloadUrl);

        await _http.DownloadToStream(
            buildInformation.DownloadUrl!,
            file,
            DownloadProgressCallback,
            cancel);

        file.Position = 0;

        Progress = null;

        Status = UpdateStatus.Verifying;

        var hash = await Task.Run(() => HashFileSha256(file), cancel);
        file.Position = 0;

        var newFileHashString = Convert.ToHexString(hash);
        if (buildInformation.Hash is { } expectHash)
        {
            if (!expectHash.Equals(newFileHashString, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Hash mismatch. Expected: {expectHash}, got: {newFileHashString}");
            }
        }

        Log.Verbose("Done downloading zip. Hash: {DownloadHash}", newFileHashString);

        return hash;
    }

    private void ZipIngest(
        SqliteConnection con,
        long versionId,
        ZipArchive zip,
        bool underlay,
        CancellationToken cancel)
    {
        var totalSize = 0L;
        var sw = new Stopwatch();

        var newFileCount = 0;

        SqliteBlobStream? blob = null;
        try
        {
            // Re-use compression buffer and compressor for all files, creating/freeing them is expensive.
            var compressBuffer = new MemoryStream();
            using var zStdCompressor = new ZStdCompressStream(compressBuffer);

            var count = 0;
            foreach (var entry in zip.Entries)
            {
                cancel.ThrowIfCancellationRequested();

                if (count++ % 100 == 0)
                    Progress = (count++, zip.Entries.Count, ProgressUnit.None);

                // Ignore directory entries.
                if (entry.Name == "")
                    continue;

                if (underlay)
                {
                    // Ignore files from the zip file we already have.
                    var exists = con.ExecuteScalar<bool>(
                        @"SELECT COUNT(*) FROM ContentManifest
                        WHERE Path = @Path AND VersionId = @VersionId",
                        new
                        {
                            Path = entry.FullName,
                            VersionId = versionId
                        }
                    );

                    if (exists)
                        continue;
                }

                // Log.Verbose("Storing file {EntryName}", entry.FullName);

                byte[] hash;
                using (var stream = entry.Open())
                {
                    hash = Blake2B.HashStream(stream, 32);
                }

                var row = con.QueryFirstOrDefault<long>(
                    "SELECT Id FROM Content WHERE Hash = @Hash",
                    new { Hash = hash });
                if (row == 0)
                {
                    newFileCount += 1;

                    // Don't have this content blob yet, insert it into the database.
                    using var entryStream = entry.Open();

                    var compress = entry.Length - entry.CompressedLength > 10;
                    if (compress)
                    {
                        sw.Start();
                        entryStream.CopyTo(zStdCompressor, (int)Zstd.ZSTD_CStreamInSize());
                        // Flush to end fragment (i.e. file)
                        zStdCompressor.FlushEnd();
                        sw.Stop();

                        totalSize += compressBuffer.Length;

                        row = con.ExecuteScalar<long>(
                            @"INSERT INTO Content(Hash, Size, Compression, Data)
                        VALUES (@Hash, @Size, @Compression, zeroblob(@BlobLen))
                        RETURNING Id",
                            new
                            {
                                Hash = hash,
                                Size = entry.Length,
                                BlobLen = compressBuffer.Length,
                                Compression = ContentCompressionScheme.ZStd
                            });

                        if (blob == null)
                            blob = SqliteBlobStream.Open(con.Handle!, "main", "Content", "Data", row, true);
                        else
                            blob.Reopen(row);

                        // Write memory buffer to SQLite and reset it.
                        blob.Write(compressBuffer.GetBuffer().AsSpan(0, (int)compressBuffer.Length));
                        compressBuffer.Position = 0;
                        compressBuffer.SetLength(0);
                    }
                    else
                    {
                        row = con.ExecuteScalar<long>(
                            @"INSERT INTO Content(Hash, Size, Compression, Data)
                            VALUES (@Hash, @Size, @Compression, zeroblob(@Size))
                            RETURNING Id",
                            new { Hash = hash, Size = entry.Length, Compression = ContentCompressionScheme.None });

                        if (blob == null)
                            blob = SqliteBlobStream.Open(con.Handle!, "main", "Content", "Data", row, true);
                        else
                            blob.Reopen(row);

                        entryStream.CopyTo(blob);
                    }
                }

                con.Execute(
                    "INSERT INTO ContentManifest(VersionId, Path, ContentId) VALUES (@VersionId, @Path, @ContentId)",
                    new
                    {
                        VersionId = versionId,
                        Path = entry.FullName,
                        ContentId = row,
                    });
            }
        }
        finally
        {
            blob?.Dispose();
        }

        Log.Debug("Compression report: {ElapsedMs} ms elapsed, {TotalSize} B total size", sw.ElapsedMilliseconds,
            totalSize);
        Log.Debug("New files: {NewFilesCount}", newFileCount);
    }
}
