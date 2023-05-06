using System;
using System.Collections.Generic;

namespace SS14.Launcher.Utility;

public static class CollectionExtensions
{
    /// <summary>
    ///     Remove an item from the list, replacing it with the one at the very end of the list.
    ///     This means that the order will not be preserved, but it should be an O(1) operation.
    /// </summary>
    /// <param name="list">The list to remove from</param>
    /// <param name="index">The index to remove</param>
    /// <returns>The removed element</returns>
    public static T RemoveSwap<T>(this IList<T> list, int index)
    {
        // This method has no implementation details,
        // and changing the result of an operation is a breaking change.
        var old = list[index];
        var replacement = list[^1];
        list[index] = replacement;
        list.RemoveAt(list.Count - 1);
        return old;
    }

    public static bool Contains<T>(this T[] array, T value) => Array.IndexOf(array, value) != -1;
}
