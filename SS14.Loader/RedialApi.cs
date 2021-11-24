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

        Process.Start(startInfo);
    }
}
