using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Serilog;
using SS14.Launcher.Models;

namespace SS14.Launcher.Views;

public sealed partial class ServerInfoLinkControl : UserControl
{
    private static readonly HashSet<string> ValidIcons = new()
    {
        "discord",
        "wiki",
        "web",
        "github",
        "forum",
        "telegram",
    };

    public ServerInfoLinkControl()
    {
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ServerInfoLink link)
            return;

        Helpers.SafeOpenServerUri(link.Url);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is not ServerInfoLink link)
            return;

        if (link.Icon == null)
            return;

        if (!ValidIcons.Contains(link.Icon))
        {
            Log.Warning("Invalid info icon: {Icon}", link.Icon);
            return;
        }

        IconLabel.Icon = (IImage)this.FindResource($"InfoIcon-{link.Icon}")!;
    }
}
