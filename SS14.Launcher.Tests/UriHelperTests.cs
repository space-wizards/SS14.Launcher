using System;
using NUnit.Framework;

namespace SS14.Launcher.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class UriHelperTests
{
    [Test]
    [TestCase("server.spacestation14.io", "http://server.spacestation14.io:1212/status")]
    [TestCase("ss14s://server.spacestation14.io", "https://server.spacestation14.io/status")]
    [TestCase("ss14s://server.spacestation14.io:1212", "https://server.spacestation14.io:1212/status")]
    [TestCase("ss14s://server.spacestation14.io/foo", "https://server.spacestation14.io/foo/status")]
    public void GetServerStatusAddress(string input, string expected)
    {
        var uri = UriHelper.GetServerStatusAddress(input);

        Assert.That(uri, Is.EqualTo(new Uri(expected)));
    }

    [Test]
    [TestCase("ss14://127.0.0.1:1212", true)]
    [TestCase("ss14s://game.ss14.io", true)]
    [TestCase("game.ss14.io:1212", true)]
    [TestCase("ss14://server.com; rm -rf /", false)]
    [TestCase("ss14://server.com && evil.exe", false)]
    [TestCase("ss14://server.com\" | evil", false)]
    [TestCase("ss14://server.com`whoami`", false)]
    [TestCase("http://malicious.com", false)]
    [TestCase("ftp://malicious.com", false)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("ss14://server.com<script>", false)]
    [TestCase("ss14://server.com>output", false)]
    [TestCase("ss14://server.com\\..\\etc\\passwd", false)]
    [TestCase("ss14://server.com\tevil", false)]
    public void TestTryParseSs14Uri_SecuritySanitation(string input, bool expectedValid)
    {
        var success = UriHelper.TryParseSs14Uri(input, out var uri);
        Assert.That(success, Is.EqualTo(expectedValid));
        if (expectedValid)
        {
            Assert.That(uri, Is.Not.Null);
        }
    }
}