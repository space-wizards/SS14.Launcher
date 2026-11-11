using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SS14.Launcher.Views;

public sealed partial class LanguageSelector : UserControl
{
    public LanguageSelector()
    {
        InitializeComponent();
    }

    // How far one notch of the mouse wheel scrolls the list, in pixels.
    private const double WheelStep = 48;

    /// <summary>
    /// The Save/Cancel/Help buttons are outside the language list's ScrollViewer, so scrolling moved the thing underneath instead.
    /// Idk if this is the way to properly do this, but it works. Shrug.
    /// </summary>
    private void OnPopupWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled)
            return;

        LanguageScroll.Offset = new Vector(LanguageScroll.Offset.X, LanguageScroll.Offset.Y - e.Delta.Y * WheelStep);
        e.Handled = true;
    }

    public PlacementMode Placement
    {
        get => DropDown.Placement;
        set => DropDown.Placement = value;
    }
}
