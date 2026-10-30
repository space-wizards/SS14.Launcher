using System;
using System.Collections.Generic;

namespace SS14.Launcher.Utility;

/// <summary>
/// Notices when errors come in faster than anything could recover from them.
/// </summary>
/// <param name="limit">How many errors within <paramref name="window"/> are too many.</param>
/// <param name="window">How far back to count.</param>
public sealed class ErrorRateLimiter(int limit, TimeSpan window)
{
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _times = new();

    /// <summary>
    /// Records an error that just happened.
    /// </summary>
    /// <returns>True if that is now too many errors in too short a time.</returns>
    public bool Record(DateTimeOffset now)
    {
        lock (_lock)
        {
            while (_times.Count > 0 && now - _times.Peek() > window)
            {
                _times.Dequeue();
            }

            _times.Enqueue(now);

            return _times.Count >= limit;
        }
    }
}
