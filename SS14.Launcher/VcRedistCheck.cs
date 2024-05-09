using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace SS14.Launcher;

// Checks that the VC++ 2015 redist, which the game needs, is present.
public static class VcRedistCheck
{
    public static unsafe void Check()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // ReSharper disable once StringLiteralTypo
        if (NativeLibrary.TryLoad("VCRUNTIME140.dll", out var lib))
        {
            // VC++ 2015 runtime present, we're good.
            // Unload the library I guess since we don't need it.
            NativeLibrary.Free(lib);

            return;
        }

        // We could show this dialog all fancy with Avalonia but I'm lazy so.
        int ret;

        var text = "The game needs the VC++ 2015 redistributable installed, which you do not have.\nWould you like to download the installer for it?";
        var caption = "VC++ 2015 redistributable not installed";
        uint type = MB.MB_ICONERROR | MB.MB_YESNO;
        {
            {
                ret = Helpers.MessageBoxHelper(text, caption, type);
            }
        }

        if (ret == Windows.IDYES)
        {
            Process.Start(new ProcessStartInfo
                {
                    FileName = "https://aka.ms/vs/16/release/vc_redist.x64.exe",
                    UseShellExecute = true
                })!
                .WaitForExit();
        }

        Environment.Exit(1);
    }
}
