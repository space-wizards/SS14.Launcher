using System;
using System.Diagnostics;
using Microsoft.Win32;
using Serilog;

namespace SS14.Launcher;

public static class ProtocolSetup
{
    public static bool CheckExisting()
    {
        var result = false;
        if (OperatingSystem.IsWindows())
        {
            using var key1 = Registry.ClassesRoot.OpenSubKey("ss14s", false);
            using var key2 = Registry.ClassesRoot.OpenSubKey("ss14", false);
            result = key1 != null && key2 != null;
        }

        if (OperatingSystem.IsMacOS())
        {
            // todo do this (1)
        }

        if (OperatingSystem.IsLinux())
        {
            // todo steam makes its own .desktop and idk if its possible to add mime types to it via steam, so this is a bit of a problem
            // this will assume you have downloaded the zip launcher
            // todo how do i get data to see the output of this
            var proc = new Process();
            proc.StartInfo.FileName = "sh";
            proc.StartInfo.Arguments = "xdg-mime default x-scheme-handler/ss14;xdg-mime default x-scheme-handler/ss14";
            proc.Start();
            // https://stackoverflow.com/questions/206323/how-to-execute-command-line-in-c-get-std-out-results
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
        }

        return result;
    }


    public static void RegisterProtocol()
    {
        // Windows registration
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var proc = new Process();
                proc.StartInfo.FileName = "Space Station 14 Launcher.exe";
                proc.StartInfo.Arguments = "--register-protocol";
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Verb = "runas";
                proc.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Do nothing, the user either declined UAC, they don't have administrator rights or something else went wrong.
                Log.Warning("User declined UAC or doesn't have admin rights.");
            }
        }
        if (OperatingSystem.IsMacOS())
        {
            // todo ditto (1)
            // https://eclecticlight.co/2019/03/25/lsregister-a-valuable-undocumented-command-for-launchservices/
            // https://ss64.com/mac/lsregister.html

            var proc = new Process();
            // Yes you have to manually go to this
            proc.StartInfo.FileName = "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister";
            proc.StartInfo.Arguments = "-f /path/to/your/app.app";
            proc.Start();
        }

        if (OperatingSystem.IsLinux())
        {
            // todo ditto (2)
            var proc = new Process();
            proc.StartInfo.FileName = "xdg-mime";
            proc.StartInfo.Arguments = "xdg-mime default SS14.desktop x-scheme-handler/ss14;xdg-mime default SS14.desktop x-scheme-handler/ss14s";
            proc.Start();
        }
    }
    public static void UnregisterProtocol()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var proc = new Process();
                proc.StartInfo.FileName = "Space Station 14 Launcher.exe";
                proc.StartInfo.Arguments = "--unregister-protocol";
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Verb = "runas";
                proc.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Do nothing, the user either declined UAC, they don't have administrator rights or something else went wrong.
                Log.Warning("User declined UAC or doesn't have admin rights.");
            }
        }
        if (OperatingSystem.IsMacOS())
        {
            // todo ditto (1)
            var proc = new Process();
            proc.StartInfo.FileName = "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister";
            proc.StartInfo.Arguments = "-u /path/to/your/app.app";
            proc.Start();
        }

        if (OperatingSystem.IsLinux())
        {
            // todo ditto (2)
        }
    }
}
