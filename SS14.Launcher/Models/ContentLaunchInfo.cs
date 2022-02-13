namespace SS14.Launcher.Models;

/// <summary>
/// Information loaded by the updater that we need to launch the game.
/// </summary>
public sealed record ContentLaunchInfo(long Version, (string Module, string Version)[] ModuleInfo);

