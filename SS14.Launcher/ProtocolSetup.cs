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
        }

        if (OperatingSystem.IsLinux())
        {
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
                proc.StartInfo.FileName = "powershell.exe";
                proc.StartInfo.Arguments = $"-executionpolicy bypass -windowstyle hidden -noninteractive -nologo " +
                                           $"-file \"{AppDomain.CurrentDomain.BaseDirectory}windows_protocol_register.ps1\"";
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
        }

        if (OperatingSystem.IsLinux())
        {
        }
    }
    public static void UnregisterProtocol()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var proc = new Process();
                proc.StartInfo.FileName = "powershell.exe";
                proc.StartInfo.Arguments = $"-executionpolicy bypass -windowstyle hidden -noninteractive -nologo " +
                                           $"-file \"{AppDomain.CurrentDomain.BaseDirectory}windows_protocol_unregister.ps1\"";
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
        }

        if (OperatingSystem.IsLinux())
        {
        }
    }
}
