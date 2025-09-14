using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SS14.Launcher.Bootstrap
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            UnfuckDotnetRoot();

            var path = AppContext.BaseDirectory;
            var ourDir = Path.GetDirectoryName(path)!;
            Debug.Assert(ourDir != null);

            var dotnetDir = Path.Combine(ourDir, "dotnet");
            var exeDir = Path.Combine(ourDir, "bin", "SS14.Launcher.exe");

            Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetDir);
            Process.Start(new ProcessStartInfo(exeDir));
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
