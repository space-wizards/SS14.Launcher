using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

namespace SS14.Launcher.Utility;

/// <summary>
/// Utility control that always hit tests in its geometry region.
/// Necessary to paper over some controls in Avalonia's default theme that have some annoying gaps otherwise.
/// </summary>
public sealed class AlwaysHitTest : Panel, ICustomHitTest
{
    public bool HitTest(Point point) => true;
}
