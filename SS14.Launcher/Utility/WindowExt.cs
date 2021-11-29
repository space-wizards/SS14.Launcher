using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace SS14.Launcher.Utility;

public static class WindowExt
{
    // https://github.com/AvaloniaUI/Avalonia/issues/2975
    public static void ActivateForReal(this WindowBase window)
    {
        if (OperatingSystem.IsWindows())
        {
            _ = SetForegroundWindow(window.PlatformImpl.Handle.Handle);
            return;
        }

        window.Activate();
    }

    [DllImport("user32.dll")]
    private static extern int SetForegroundWindow(IntPtr hWnd);
}
