using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;
using Serilog;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Views;

namespace SS14.Launcher;

public abstract class Protocol
{
    private static bool CheckExisting()
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
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
        }

        return result;
    }
    public static async Task<ProtocolsResultCode> RegisterProtocol()
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
                return ProtocolsResultCode.ErrorWindowsUac;
            }
        }
        // macOS registration
        if (OperatingSystem.IsMacOS())
        {
            var path = $"{AppDomain.CurrentDomain.BaseDirectory}";

            // User needs to move the app manually to get this sandbox restriction lifted. This can be done "automated" by making one of those installer dmg stuff
            if (path.Contains("AppTranslocation"))
            {
                Log.Error("I have been put in apple jail (Gatekeeper path randomisation)... move me to your application folder");
                return ProtocolsResultCode.ErrorMacOSTranslocation;
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
        return ProtocolsResultCode.Success;
    }
    public static async Task<ProtocolsResultCode> UnregisterProtocol()
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
                return ProtocolsResultCode.ErrorWindowsUac;
            }
        }
        // macOS unregistration
        if (OperatingSystem.IsMacOS())
        {
            // This just... seems to do nothing. Its correct to my documentation...
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
        return ProtocolsResultCode.Success;
    }

    // UI popup stuff
    public static async Task OptionsManualPopup(MainWindow control)
    {
        var existing = CheckExisting();

        // Not using ConfirmDialogBuilder because I am not sure how to make it support variables or whatever they are named
        var dialog = new ConfirmDialog
        {
            Title = LocalizationManager.Instance.GetString("protocols-dialog-title"),
            DialogContent = LocalizationManager.Instance.GetString("protocols-dialog-content-action-question",
                ("action", existing ? LocalizationManager.Instance.GetString("protocols-dialog-action-register")
                    : LocalizationManager.Instance.GetString("protocols-dialog-action-unregister"))),
            ConfirmButtonText = LocalizationManager.Instance.GetString("protocols-dialog-continue"),
            CancelButtonText = LocalizationManager.Instance.GetString("protocols-dialog-back"),
        };

        var question = await dialog.ShowDialog<bool>(control);

        if (question)
        {
            var result = existing ? await UnregisterProtocol() : await RegisterProtocol();
            await HandleResult(result, control);
        }
    }

    public static async Task ProtocolSignupPopup(MainWindow control, DataManager cfg)
    {
        if (IsCandidateForProtocols(cfg))
            return;

        var answer = await Helpers.ConfirmDialogBuilder(control,
            "protocols-dialog-title",
            "protocols-dialog-content",
            "protocols-dialog-confirm",
            "protocols-dialog-deny");

        if (answer)
        {
            await HandleResult(await RegisterProtocol(), control);
        }

        cfg.SetCVar(CVars.HasSeenProtocolsDialog, true);
    }

    private static async Task HandleResult(ProtocolsResultCode result, MainWindow control)
    {
        retryPoint:

        switch (result)
        {
            case ProtocolsResultCode.Success:
                await Helpers.OkDialogBuilder(control,
                    "protocols-dialog-title",
                    "protocols-dialog-content-success",
                    "protocols-dialog-ok");
                break;
            case ProtocolsResultCode.ErrorWindowsUac:
                var retryUac = await Helpers.ConfirmDialogBuilder(control,
                    "protocols-dialog-error-title",
                    "protocols-dialog-error-windows-uac",
                    "protocols-dialog-error-again",
                    "protocols-dialog-deny");
                if (retryUac)
                    goto retryPoint;
                break;
            case ProtocolsResultCode.ErrorMacOSTranslocation:
                await Helpers.OkDialogBuilder(control,
                    "protocols-dialog-error-title",
                    "protocols-dialog-error-macos-translocation",
                    "protocols-dialog-error-ok");
                break;
            case ProtocolsResultCode.ErrorUnknown:
                var retryUnknown = await Helpers.ConfirmDialogBuilder(control,
                    "protocols-dialog-error-title",
                    "protocols-dialog-error-generic",
                    "protocols-dialog-error-again",
                    "protocols-dialog-deny");
                if (retryUnknown)
                    goto retryPoint;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static bool IsCandidateForProtocols(DataManager cfg)
    {
        // They have been shown this dialog before, don't bother.
        if (cfg.GetCVar(CVars.HasSeenProtocolsDialog))
            return false;

        // It already exists. Either cause of a reset config file or already installed by steam.
        // Let's also set the cvar.
        if (CheckExisting())
        {
            cfg.SetCVar(CVars.HasSeenProtocolsDialog, true);

            return false;
        }

        // Check if the OS is compatible... im sorry freebsd users
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            return false;

        // We (hopefully) are ready!
        return true;
    }

    public enum ProtocolsResultCode : byte
    {
        Success =  0,
        ErrorWindowsUac,
        ErrorMacOSTranslocation,
        ErrorUnknown,
    }
}
