using System;
using System.Diagnostics;
using TerraFX.Interop.Windows;

namespace SS14.Launcher.Utility;

/// <summary>
/// Shows a fatal error to the user without needing the launcher's UI, for failures that happen before the
/// UI exists (or after it has died).
/// </summary>
/// <remarks>
/// Best effort, Windows should always work, but Linux can be a huge maybe.
/// The text is English because localization may not be loaded and we can't count on it being correct if it is.
/// </remarks>
public static class FatalErrorDialog
{
    private const string Caption = "Space Station 14 Launcher";

    public static void Show(ErrorCode code, string headline)
    {
        string logPath;
        try
        {
            logPath = LauncherPaths.DirLogs;
        }
        catch (Exception)
        {
            logPath = "(unknown)";
        }

        var text = $"{headline}\n\n" +
                   $"Error code: {code}\n\n" +
                   "Include this code when asking for help.\n" +
                   $"Details were written to the launcher logs in:\n{logPath}";

        Console.Error.WriteLine(text);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Helpers.MessageBoxHelper(text, Caption, MB.MB_OK | MB.MB_ICONERROR);
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                var script = $"display dialog \"{EscapeAppleScript(text)}\" with title \"{Caption}\" " +
                             "buttons {\"OK\"} default button \"OK\" with icon stop";

                if (TryRun("osascript", "-e", script))
                    return;
            }
            else
            {
                // Me when I launch SS14 on my Hetzner VPS.
                if (!HasDisplay())
                    return;

                // zenity treats the text as markup, fun
                if (TryRun("zenity", "--error", "--title", Caption, "--text", EscapeMarkup(text)))
                    return;

                if (TryRun("kdialog", "--error", text, "--title", Caption))
                    return;
            }
        }
        catch (Exception)
        {
            // Nothing sensible left to do, the text is on stderr already.
            // Entirely a skill issue.
        }
    }

    private static bool HasDisplay()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
               || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    private static bool TryRun(string fileName, params string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit();

            // 0 is OK and 1 is closed, which is what these dialogs give. Anything else means it couldn't show up.
            return process.ExitCode is 0 or 1;
        }
        catch (Exception)
        {
            // Oops, no dialogue!
            return false;
        }
    }

    private static string EscapeAppleScript(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
