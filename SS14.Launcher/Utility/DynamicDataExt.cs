using System.Diagnostics.CodeAnalysis;
using DynamicData;

namespace SS14.Launcher.Utility;

public static class DynamicDataExt
{
    public static bool TryLookup<TValue, TKey>(
        this IObservableCache<TValue, TKey> cache,
        TKey key,
        [NotNullWhen(false)] out TValue value)
        where TKey : notnull
    {
        var option = cache.Lookup(key);
        if (option.HasValue)
        {
            value = option.Value;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryLookup<TValue, TKey>(
        this SourceCache<TValue, TKey> cache,
        TKey key,
        [NotNullWhen(false)] out TValue value)
        where TKey : notnull
    {
        var option = cache.Lookup(key);
        if (option.HasValue)
        {
            value = option.Value;
            return true;
        }

        value = default;
        return false;
    }
}
