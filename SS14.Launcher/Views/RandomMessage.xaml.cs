using Avalonia.Controls;
using Splat;
using SS14.Launcher.Models;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Views;

public sealed partial class RandomMessage : UserControl
{
    public RandomMessage()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        Text.Text = Locator.Current.GetRequiredService<LauncherInfoManager>().GetRandomMessage();
    }
}
