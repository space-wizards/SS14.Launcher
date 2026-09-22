using System.Diagnostics;
using NUnit.Framework;
using SS14.Launcher.Models;

namespace SS14.Launcher.Tests;

[TestFixture]
public class ConnectorTests
{
    [Test]
    public void MacOsLoaderForwardsSteamInjectionPastHelpers()
    {
        const string variable = "DYLD_INSERT_LIBRARIES";
        const string libraries = "/tmp/steamloader.dylib";
        var startInfo = new ProcessStartInfo();
        startInfo.EnvironmentVariables[variable] = libraries;

        Connector.ForwardSteamInjectionPastMacOsHelper(startInfo);

        Assert.That(startInfo.EnvironmentVariables.ContainsKey(variable), Is.False);
        Assert.That(startInfo.EnvironmentVariables["SS14_STEAM_DYLD_INSERT_LIBRARIES"],
            Is.EqualTo(libraries));
    }
}
