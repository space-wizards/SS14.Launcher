using System;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

/// <summary>
/// One entry in the development tab's list of ways to make the launcher fail.
/// </summary>
/// <param name="Name">What to show in the list.</param>
/// <param name="Run">Makes it happen.</param>
public sealed record DevelopmentFailure(string Name, Action Run);
