using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SS14.Launcher;

/// <summary>
///     Contains file paths used by the launcher to manage data.
/// </summary>
public static class LauncherPaths
{
    public static readonly string AppDataPath = Path.Combine("Space Station 14", GetAppDataName());
    public static readonly string EngineInstallationsDirName = "engines";
    public static readonly string EngineModulesDirName = "modules";
    public static readonly string ServerContentDirName = "server content";
    public static readonly string LogsDirName = "logs";
    public static readonly string LauncherLogName = "launcher.log";
    public static readonly string ClientMacLogName = "client.mac.log";
    public static readonly string ClientStdoutLogName = "client.stdout.log";
    public static readonly string ClientStderrLogName = "client.stderr.log";

    public static readonly string DirLauncherInstall = GetInstallDir();
    public static readonly string DirUserData = GetUserDataDir();
    public static readonly string DirLocalData = GetLocalUserDataDir();
    public static readonly string DirEngineInstallations = Path.Combine(DirUserData, EngineInstallationsDirName);
    public static readonly string DirModuleInstallations = Path.Combine(DirUserData, EngineModulesDirName);
    // Legacy server content directory. No longer used except to delete on launch.
    public static readonly string DirServerContent = Path.Combine(DirUserData, ServerContentDirName);
    public static readonly string DirLogs = Path.Combine(DirUserData, LogsDirName);
    public static readonly string PathLauncherLog = Path.Combine(DirLogs, LauncherLogName);
    public static readonly string PathClientMacLog = Path.Combine(DirLogs, ClientMacLogName);
    public static readonly string PathClientStdoutLog = Path.Combine(DirLogs, ClientStdoutLogName);
    public static readonly string PathClientStderrLog = Path.Combine(DirLogs, ClientStderrLogName);
    public static readonly string PathPublicKey = Path.Combine(DirLauncherInstall, "signing_key");

    public static void CreateDirs()
    {
        Ensure(DirLogs);
        Ensure(DirLocalData);
        Ensure(DirEngineInstallations);
        Ensure(DirModuleInstallations);

        static void Ensure(string path) => Helpers.EnsureDirectoryExists(path);
    }

    private static string GetInstallDir()
    {
        return Path.GetDirectoryName(typeof(LauncherPaths).Assembly.Location)!;
    }

    private static string GetUserDataDir()
    {
        string appDataDir;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (xdgDataHome == null)
            {
                appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            }
            else
            {
                appDataDir = xdgDataHome;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else
        {
            appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        return Path.Combine(appDataDir, AppDataPath);
    }

    private static string GetLocalUserDataDir()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, AppDataPath);
        }

        return GetUserDataDir();
    }

    private static string GetAppDataName()
    {
        var envVar = Environment.GetEnvironmentVariable("SS14_LAUNCHER_APPDATA_NAME");
        if (!string.IsNullOrEmpty(envVar))
            return envVar;

        return "launcher";
    }
}
