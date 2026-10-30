using System;
using NUnit.Framework;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ErrorRateLimiterTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void FewErrorsAreFine()
    {
        var limiter = new ErrorRateLimiter(5, TimeSpan.FromSeconds(10));

        for (var i = 0; i < 4; i++)
        {
            Assert.That(limiter.Record(Start.AddSeconds(i)), Is.False);
        }
    }

    [Test]
    public void TooManyInARowIsTooMany()
    {
        var limiter = new ErrorRateLimiter(5, TimeSpan.FromSeconds(10));

        for (var i = 0; i < 4; i++)
        {
            limiter.Record(Start.AddSeconds(i));
        }

        Assert.That(limiter.Record(Start.AddSeconds(4)), Is.True);
    }

    [Test]
    public void ErrorsSpreadOutOverTimeAreFine()
    {
        var limiter = new ErrorRateLimiter(5, TimeSpan.FromSeconds(10));

        for (var i = 0; i < 50; i++)
        {
            Assert.That(limiter.Record(Start.AddSeconds(i * 3)), Is.False, $"error number {i}");
        }
    }

    [Test]
    public void OldErrorsStopCounting()
    {
        var limiter = new ErrorRateLimiter(3, TimeSpan.FromSeconds(10));

        limiter.Record(Start);
        limiter.Record(Start.AddSeconds(1));

        Assert.That(limiter.Record(Start.AddSeconds(60)), Is.False);
    }

    [Test]
    public void LimitCountsTheErrorThatReachesIt()
    {
        var limiter = new ErrorRateLimiter(1, TimeSpan.FromSeconds(10));

        Assert.That(limiter.Record(Start), Is.True);
    }
}
