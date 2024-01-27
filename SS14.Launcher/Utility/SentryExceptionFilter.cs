using System;
using System.Collections.Generic;
using Avalonia.Threading;
using Sentry;
using SS14.Launcher.Views;

// ReSharper disable LoopCanBeConvertedToQuery

namespace SS14.Launcher.Utility;

public static class SentryExceptionFilter
{
    public static WeakReference<MainWindow>? Window { set; get; }

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

        var result = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var feedbackForm = new SentryFeedbackWindow();
            if (Window == null || !Window.TryGetTarget(out var window))
                return null;

            var feedback = await feedbackForm.ShowDialog<string?>(window);
            return feedback;
        });

        result.Wait();
        if (result.Result != null && sentryEvent != null)
        {
            SentrySdk.CaptureUserFeedback(sentryEvent.EventId, "", result.Result);
        }

        return sentryEvent;
    }
}
