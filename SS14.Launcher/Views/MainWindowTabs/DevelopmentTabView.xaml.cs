using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Serilog;

namespace SS14.Launcher.Views.MainWindowTabs;

public sealed partial class DevelopmentTabView : UserControl
{
    public DevelopmentTabView()
    {
        InitializeComponent();
    }

    private async void RegisterProtocols(object? _1, RoutedEventArgs _2)
    {
        try
        {
            await Protocol.RegisterProtocol();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error registering protocols");
        }
    }

    private async void UnregisterProtocols(object? _1, RoutedEventArgs _2)
    {
        try
        {
            await Protocol.UnregisterProtocol();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error unregistering protocols");
        }
    }
}
