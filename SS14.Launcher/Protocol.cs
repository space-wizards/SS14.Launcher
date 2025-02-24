using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;
using Serilog;

namespace SS14.Launcher;

public static class Protocol
{
    public static bool CheckExisting()
    {
        var result = false;
        if (OperatingSystem.IsWindows())
        {
            using var key1 = Registry.ClassesRoot.OpenSubKey("ss14s", false);
            using var key2 = Registry.ClassesRoot.OpenSubKey("ss14", false);
            using var key3 = Registry.ClassesRoot.OpenSubKey("RobustToolbox", false);
            // no need to check the extension keys, they point to RobustToolbox
            result = key1 != null && key2 != null && key3 != null;
        }

        if (OperatingSystem.IsMacOS())
        {
            // todo macos check existing protocol setup
            // I got no idea how to do this, lsregister does not report anything.
            // Lets just assume theres no record
        }

        if (OperatingSystem.IsLinux())
        {
            // todo steam makes its own .desktop and idk if its possible to add mime types to it via steam, so this is a bit of a problem
            // this will assume you have downloaded the zip launcher
            // todo how do i get data to see the output of this
            var proc = new Process();
            proc.StartInfo.FileName = "xdg-mime";
            proc.StartInfo.Arguments = "default x-scheme-handler/ss14;xdg-mime default x-scheme-handler/ss14";
            proc.Start();
            // https://stackoverflow.com/questions/206323/how-to-execute-command-line-in-c-get-std-out-results
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
        }

        return result;
    }
    public static Task<bool> RegisterProtocol()
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
                return Task.FromResult(false);
            }
        }
        // macOS registration
        if (OperatingSystem.IsMacOS())
        {
            // Yeah you cant get this to work on dev builds
            #if !FULL_RELEASE
            return Task.FromResult(false);
            #endif
            var path = $"{AppDomain.CurrentDomain.BaseDirectory}";

            // User needs to move the app manually to get this sandbox restriction lifted. This can be done "automated" by making one of those installer dmg stuff
            if (path.Contains("AppTranslocation"))
            {
                Log.Error("I have been put in apple jail (Gatekeeper path randomisation)... move me to your application folder");
                return Task.FromResult(false);
            }

            var newPath = string.Empty;
            var appIndex = path.IndexOf(".app", StringComparison.Ordinal);
            if (appIndex >= 0)
            {
                newPath = path.Substring(0, appIndex + 4);
            }

            var proc = new Process();
            // Yes you have to manually go to this
            proc.StartInfo.FileName = "/System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/LaunchServices.framework/Versions/A/Support/lsregister";
            proc.StartInfo.Arguments = $"-R -f {newPath}";
            proc.Start();
        }
        // Linux registration
        if (OperatingSystem.IsLinux())
        {
            var desktopfile = "";

            // todo ditto (2)
            var proc = new Process();
            proc.StartInfo.FileName = "xdg-mime";
            proc.StartInfo.Arguments = $"default {desktopfile} x-scheme-handler/ss14;xdg-mime default SS14.desktop x-scheme-handler/ss14s";
            proc.Start();
        }

        Log.Information("Successfully registered protocol");
        return Task.FromResult(true);
    }
    public static Task<bool> UnregisterProtocol()
    {
        // Windows unregistration
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
                return Task.FromResult(false);
            }
        }
        // macOS unregistration
        if (OperatingSystem.IsMacOS())
        {
            // This just... seems to do nothing. Its correct to my documentation...
            #if !FULL_RELEASE
            return Task.FromResult(false);
            #endif
            Log.Information("Unregistering protocol for macos...");
            var path = $"{AppDomain.CurrentDomain.BaseDirectory}";

            var newPath = string.Empty;
            var appIndex = path.IndexOf(".app", StringComparison.Ordinal);
            if (appIndex >= 0)
            {
                newPath = path.Substring(0, appIndex + 4);
            }

            var proc = new Process();
            proc.StartInfo.FileName = "/System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/LaunchServices.framework/Versions/A/Support/lsregister";
            proc.StartInfo.Arguments = $"-R -f -u {newPath}";
            proc.Start();
        }
        // Linux unregistration
        if (OperatingSystem.IsLinux())
        {
            // todo ditto (2)
        }
        Log.Information("Successfully unregistered protocol");

        return Task.FromResult(true);
    }
}
