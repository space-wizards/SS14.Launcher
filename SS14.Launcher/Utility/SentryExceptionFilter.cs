using System.Collections.Generic;
using Sentry;
// ReSharper disable LoopCanBeConvertedToQuery

namespace SS14.Launcher.Utility;

public static class SentryExceptionFilter
{
    private static IEnumerable<string> IgnoredSentryMessageFilter => new[]
    {
        "HappyEyeballsHttp"
    };

    public static void SetupFilters(this SentryOptions options)
    {
        options.SetBeforeSend(FilterExceptions);
    }

    private static SentryEvent? FilterExceptions(SentryEvent? sentryEvent, Hint hint)
    {
        foreach (var excluded in IgnoredSentryMessageFilter)
        {
            if (sentryEvent?.Message?.Message?.Contains(excluded) ?? false)
                return null;
        }

        return sentryEvent;
    }
}
