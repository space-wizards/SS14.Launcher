using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Robust.LoaderApi;

namespace SS14.Loader;

internal sealed class SqliteContentFileApi : IFileApi
{
    public bool TryOpen(string path, [NotNullWhen(true)] out Stream? stream)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> AllFiles => throw new NotImplementedException();
}
