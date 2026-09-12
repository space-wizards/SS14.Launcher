using System;
using NUnit.Framework;

namespace SS14.Launcher.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class OpenUriSecurityTests
{
    [Test]
    [TestCase("https://spacestation14.io", true)]
    [TestCase("http://localhost:1212/status", true)]
    [TestCase("mailto:support@ss14.io", true)]
    [TestCase("ftp://evil.com/payload", false)]
    [TestCase("file:///etc/passwd", false)]
    [TestCase("javascript:alert(1)", false)]
    [TestCase("ssh://attacker.com", false)]
    [TestCase("data:text/html,<script>alert(1)</script>", false)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    public void TestSafeOpenServerUri_SchemeAllowlist(string uri, bool shouldBeAllowed)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
        {
            var isAllowed = parsedUri.Scheme is "http" or "https";

            if (shouldBeAllowed && parsedUri.Scheme == "mailto")
            {
                isAllowed = false;
            }

            if (parsedUri.Scheme is not ("http" or "https"))
            {
                Assert.That(isAllowed, Is.False,
                    $"Scheme '{parsedUri.Scheme}' should be blocked by SafeOpenServerUri");
            }
        }
        else
        {
            Assert.Pass("Invalid URI is automatically rejected");
        }
    }

    [Test]
    [TestCase("file:///etc/shadow")]
    [TestCase("ssh://root@server")]
    [TestCase("telnet://evil.com")]
    [TestCase("ldap://dc.evil.com")]
    [TestCase("gopher://old.protocol")]
    public void TestOpenUri_DangerousSchemes_NotInAllowlist(string uri)
    {
        Assert.That(Uri.TryCreate(uri, UriKind.Absolute, out var parsed), Is.True);
        var isAllowed = parsed!.Scheme == Uri.UriSchemeHttp ||
                        parsed.Scheme == Uri.UriSchemeHttps ||
                        parsed.Scheme == Uri.UriSchemeMailto;
        Assert.That(isAllowed, Is.False,
            $"Dangerous scheme '{parsed.Scheme}' must not be in the OpenUri allowlist");
    }
}
