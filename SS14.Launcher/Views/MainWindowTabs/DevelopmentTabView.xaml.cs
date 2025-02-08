using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SS14.Launcher.Views.MainWindowTabs;

public sealed partial class DevelopmentTabView : UserControl
{
    public DevelopmentTabView()
    {
        InitializeComponent();
    }

    private void RegisterProtocols(object? _1, RoutedEventArgs _2)
    {
        Protocol.RegisterProtocol();
    }

    private void UnregisterProtocols(object? _1, RoutedEventArgs _2)
    {
        Protocol.UnregisterProtocol();
    }
}
