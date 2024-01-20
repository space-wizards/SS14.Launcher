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
                var key1 = Registry.ClassesRoot.CreateSubKey("ss14s");
                key1!.SetValue("URL Protocol", "");
                key1 = key1.CreateSubKey("Shell\\Open\\Command");
                key1!.SetValue("", $"\"{AppDomain.CurrentDomain.BaseDirectory}Space Station 14 Launcher.exe\" \"%1\"");
                key1.Close();

                var key2 = Registry.ClassesRoot.CreateSubKey("ss14");
                key2!.SetValue("URL Protocol", "");
                key2 = key2.CreateSubKey("Shell\\Open\\Command");
                key2!.SetValue("", $"\"{AppDomain.CurrentDomain.BaseDirectory}Space Station 14 Launcher.exe\" \"%1\"");
                key2.Close();
                Environment.Exit(0);
            }
            if (args.Contains("--unregister-protocol"))
            {
                Registry.ClassesRoot.DeleteSubKeyTree("ss14s");
                Registry.ClassesRoot.DeleteSubKeyTree("ss14");
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
                var arguments = string.Join(" ", args);
                Process.Start(new ProcessStartInfo(exeDir, arguments));
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
