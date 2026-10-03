using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Robust.LoaderApi;

namespace SS14.Loader;

internal sealed class ZipFileApi : IFileApi, IDisposable
{
    private readonly ZipArchive _archive;
    private readonly string? _prefix;

    public ZipFileApi(ZipArchive archive, string? prefix)
    {
        _archive = archive;
        _prefix = prefix;

        /*foreach (var entry in _archive.Entries)
        {
            Console.WriteLine(entry.FullName);
        }*/
    }

    public bool TryOpen(string path, [NotNullWhen(true)] out Stream? stream)
    {
        // Console.WriteLine("LOADING: {0}", path);

        var entry = _archive.GetEntry(_prefix != null ? _prefix + path : path);
        if (entry == null)
        {
            stream = null;
            return false;
        }

        stream = new MemoryStream();
        lock (_archive)
        {
            using var zipStream = entry.Open();
            zipStream.CopyTo(stream);
        }

        stream.Position = 0;
        return true;
    }

    public IEnumerable<string> AllFiles
    {
        get
        {
            if (_prefix != null)
            {
                return _archive.Entries
                    .Where(e => e.Name != "" && e.FullName.StartsWith(_prefix))
                    .Select(e => e.FullName[_prefix.Length..]);
            }
            return _archive.Entries
                .Where(e => e.Name != "")
                .Select(entry => entry.FullName);
        }
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}
