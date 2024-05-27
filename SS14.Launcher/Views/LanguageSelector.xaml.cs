using Avalonia.Controls;

namespace SS14.Launcher.Views;

public sealed partial class LanguageSelector : UserControl
{
    public LanguageSelector()
    {
        InitializeComponent();
    }

    public PlacementMode Placement
    {
        get => DropDown.Placement;
        set => DropDown.Placement = value;
    }
}
