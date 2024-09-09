using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;
using Robust.LoaderApi;

namespace SS14.Loader;

internal sealed class RedialApi : IRedialApi
{
    // We have to reset these env vars to avoid leaking through state on redial.
    private static readonly string[] EnvVarsToClear = [
        // Robust config
        "ROBUST_AUTH_TOKEN",
        "ROBUST_AUTH_USERID",
        "ROBUST_AUTH_PUBKEY",
        "ROBUST_AUTH_SERVER",

        // Launcher config.
        "SS14_LOADER_CONTENT_DB",
        "SS14_LOADER_CONTENT_VERSION",
        "SS14_DISABLE_SIGNING",
        "SS14_LAUNCHER_PATH",
        "SS14_LOG_CLIENT",

        // .NET config
        "DOTNET_MULTILEVEL_LOOKUP",

        // .NET performance config.
        "DOTNET_TieredPGO",
        "DOTNET_TC_QuickJitForLoops",
        "DOTNET_ReadyToRun",
        "DOTNET_gcServer",
    ];

    private readonly string _launcher;

    public RedialApi(string launcher)
    {
        _launcher = launcher;
    }

    public void Redial(Uri uri, string text = "")
    {
        var reasonCommand = "R" + Convert.ToHexString(Encoding.UTF8.GetBytes(text));
        var connectCommand = "C" + Convert.ToHexString(Encoding.UTF8.GetBytes(uri.ToString()));

        var startInfo = new ProcessStartInfo
        {
            FileName = _launcher,
            UseShellExecute = false,
            ArgumentList =
            {
                "--commands",
                ":RedialWait",
                reasonCommand,
                connectCommand
            }
        };

        foreach (var envVar in EnvVarsToClear)
        {
            startInfo.EnvironmentVariables.Remove(envVar);
        }

        Process.Start(startInfo);
    }
}
