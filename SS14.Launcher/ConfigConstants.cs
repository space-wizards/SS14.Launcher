using System;

namespace SS14.Launcher;

public static class ConfigConstants
{
    public const string CurrentLauncherVersion = "15";
    public static readonly bool DoVersionCheck = true;

    // Refresh login tokens if they're within <this much> of expiry.
    public static readonly TimeSpan TokenRefreshThreshold = TimeSpan.FromDays(15);

    // If the user leaves the launcher running for absolute ages, this is how often we'll update his login tokens.
    public static readonly TimeSpan TokenRefreshInterval = TimeSpan.FromDays(7);

    // Check the command queue this often.
    public static readonly TimeSpan CommandQueueCheckInterval = TimeSpan.FromSeconds(1);

    public const string HubUrl = "https://central.spacestation14.io/hub/";
    public const string AuthUrl = "https://central.spacestation14.io/auth/";
    public const string DiscordUrl = "https://discord.gg/t2jac3p";
    public const string WebsiteUrl = "https://spacestation14.io";
    public const string DownloadUrl = "https://spacestation14.io/about/nightlies/";
    public const string LauncherVersionUrl = "https://central.spacestation14.io/launcher_version.txt";
    public const string RobustBuildsManifest = "https://central.spacestation14.io/builds/robust/manifest.json";
}
