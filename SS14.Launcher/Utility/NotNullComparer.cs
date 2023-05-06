using System.Collections.Generic;

namespace SS14.Launcher.Utility;

/// <summary>
/// Wrapper around <see cref="IComparer{T}"/> that handles nulls for you.
/// </summary>
/// <typeparam name="T">Type of item that is compared.</typeparam>
public abstract class NotNullComparer<T> : IComparer<T?> where T : notnull
{
    public abstract int Compare(T x, T y);

    int IComparer<T?>.Compare(T? x, T? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (ReferenceEquals(null, y))
            return 1;

        if (ReferenceEquals(null, x))
            return -1;

        return Compare(x, y);
    }
}
