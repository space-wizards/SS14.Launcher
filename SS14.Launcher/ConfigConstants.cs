using System;

namespace SS14.Launcher;

public static class ConfigConstants
{
    public const string CurrentLauncherVersion = "25";
    public static readonly bool DoVersionCheck = true;

    // Refresh login tokens if they're within <this much> of expiry.
    public static readonly TimeSpan TokenRefreshThreshold = TimeSpan.FromDays(15);

    // If the user leaves the launcher running for absolute ages, this is how often we'll update his login tokens.
    public static readonly TimeSpan TokenRefreshInterval = TimeSpan.FromDays(7);

    // The amount of time before a server is considered timed out for status checks.
    public static readonly TimeSpan ServerStatusTimeout = TimeSpan.FromSeconds(5);

    // Check the command queue this often.
    public static readonly TimeSpan CommandQueueCheckInterval = TimeSpan.FromSeconds(1);

    public const string LauncherCommandsNamedPipeName = "SS14.Launcher.CommandPipe";
    // Amount of time to wait before the launcher decides to ignore named pipes entirely to keep the rest of the launcher functional.
    public const int LauncherCommandsNamedPipeTimeout = 150;
    // Amount of time to wait to let a redialling client properly die
    public const int LauncherCommandsRedialWaitTimeout = 1000;

    public static readonly string AuthUrl = "https://central.spacestation14.io/auth/";
    public const string HubUrl = "https://central.spacestation14.io/hub/";
    public const string DiscordUrl = "https://discord.ss14.io/";
    public const string AccountManagementUrl = "https://central.spacestation14.io/web/Identity/Account/Manage";
    public const string WebsiteUrl = "https://spacestation14.io";
    public const string DownloadUrl = "https://spacestation14.io/about/nightlies/";
    public const string LauncherVersionUrl = "https://central.spacestation14.io/launcher_version.txt";
    public const string RobustBuildsManifest = "https://central.spacestation14.io/builds/robust/manifest.json";
    public const string RobustModulesManifest = "https://central.spacestation14.io/builds/robust/modules.json";

    static ConfigConstants()
    {
        var envVarAuthUrl = Environment.GetEnvironmentVariable("SS14_LAUNCHER_OVERRIDE_AUTH");
        if (!string.IsNullOrEmpty(envVarAuthUrl))
            AuthUrl = envVarAuthUrl;
    }
}
