using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Linq;
using System.Security;
using System.Security.Principal;

namespace SS14.Launcher.Bootstrap
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            UnfuckDotnetRoot();

            if (args.Contains("--register-protocol"))
            {
                // ss14s://
                var key = Registry.ClassesRoot.CreateSubKey("ss14s");
                key!.SetValue("URL Protocol", "Space Station 14 Secure protocol");
                key = key.CreateSubKey("Shell\\Open\\Command");
                key!.SetValue("", $"\"{AppDomain.CurrentDomain.BaseDirectory}Space Station 14 Launcher.exe\" \"%1\"");
                key.Close();

                // ss14://
                key = Registry.ClassesRoot.CreateSubKey("ss14");
                key!.SetValue("URL Protocol", "Space Station 14 protocol");
                key = key.CreateSubKey("Shell\\Open\\Command");
                key!.SetValue("", $"\"{AppDomain.CurrentDomain.BaseDirectory}Space Station 14 Launcher.exe\" \"%1\"");
                key.Close();

                // RobustToolbox (required for the file extensions)
                key = Registry.ClassesRoot.CreateSubKey("RobustToolbox");
                key!.SetValue("", "Robust Toolbox Bundle File");
                var icon = key.CreateSubKey("DefaultIcon");
                icon!.SetValue("", $"{AppDomain.CurrentDomain.BaseDirectory}Space Station 14 Launcher.exe");
                key = key.CreateSubKey("Shell\\Open\\Command");
                key!.SetValue("", $"\"{AppDomain.CurrentDomain.BaseDirectory}Space Station 14 Launcher.exe\" \"%1\"");
                key.Close();

                // .rtbundle
                key = Registry.ClassesRoot.CreateSubKey(".rtbundle");
                key!.SetValue("", "RobustToolbox");
                key.Close();

                // .rtreplay
                key = Registry.ClassesRoot.CreateSubKey(".rtreplay");
                key!.SetValue("", "RobustToolbox");
                key.Close();

                Environment.Exit(0);
            }
            if (args.Contains("--unregister-protocol"))
            {
                Registry.ClassesRoot.DeleteSubKeyTree("ss14s");
                Registry.ClassesRoot.DeleteSubKeyTree("ss14");
                Registry.ClassesRoot.DeleteSubKeyTree("RobustToolbox");
                Registry.ClassesRoot.DeleteSubKeyTree(".rtbundle");
                Registry.ClassesRoot.DeleteSubKeyTree(".rtreplay");
                Environment.Exit(0);
            }

            var path = typeof(Program).Assembly.Location;
            var ourDir = Path.GetDirectoryName(path);
            Debug.Assert(ourDir != null);

            var dotnetDir = Path.Combine(ourDir, "dotnet");
            var exeDir = Path.Combine(ourDir, "bin", "SS14.Launcher.exe");

            Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetDir);
            if (args.Length > 0)
            {
                // blursed
                // thanks anonymous for how to make args pass in properly
                Process.Start(new ProcessStartInfo(exeDir, string.Join("", args.Select((str) => $"\"{str}\" "))));
            }
            else
            {
                Process.Start(new ProcessStartInfo(exeDir));
            }
        }

        private static void UnfuckDotnetRoot()
        {
            //
            // We ship a simple console.bat script that runs the game with cmd prompt logging,
            // in a worst-case of needing logging.
            //
            // Well it turns out I dared copy paste "SETX" from StackOverflow instead of "SET".
            // The former permanently alters the user's registry to set the environment variable
            //
            // WHY THE FUCK IS THAT SO EASY TO DO???
            // AND WHY ARE PEOPLE ON STACKOVERFLOW POSTING SOMETHING SO DANGEROUS WITHOUT ASTERISK???
            //
            // Anyways, we have to fix our goddamn mess now. Ugh.
            // Try to clear that registry key if somebody previously ran console.bat and it corrupted their system.
            //

            try
            {
                using var envKey = Registry.CurrentUser.OpenSubKey("Environment", true);
                var val = envKey?.GetValue("DOTNET_ROOT");
                if (val is not string s)
                    return;

                if (!s.Contains("Space Station 14") && !s.Contains("SS14.Launcher"))
                    return;

                envKey.DeleteValue("DOTNET_ROOT");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while trying to fix DOTNET_ROOT env var: {e}");
            }
        }
    }
}
