using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using Serilog;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class OptionsTabView : UserControl
{
    public OptionsTabView()
    {
        InitializeComponent();

        Flip.Command = ReactiveCommand.Create(() =>
        {
            var window = (Window?) VisualRoot;
            if (window == null)
                return;

            window.Classes.Add("DoAFlip");

            DispatcherTimer.RunOnce(() => { window.Classes.Remove("DoAFlip"); }, TimeSpan.FromSeconds(1));
        });
    }

    public async void ClearEnginesPressed(object? _1, RoutedEventArgs _2)
    {
        ((OptionsTabViewModel)DataContext!).ClearEngines();
        await ClearEnginesButton.DisplayDoneMessage();
    }

    public async void ClearServerContentPressed(object? _1, RoutedEventArgs _2)
    {
        ((OptionsTabViewModel)DataContext!).ClearServerContent();
        await ClearServerContentButton.DisplayDoneMessage();
    }

    private async void OpenHubSettings(object? sender, RoutedEventArgs args)
    {
        await new HubSettingsDialog().ShowDialog(this.GetVisualRoot() as Window);
    }

    private async void RegisterProtocols(object? _1, RoutedEventArgs _2)
    {
        ProtocolSetup.RegisterProtocol();
    }

    private async void UnregisterProtocols(object? _1, RoutedEventArgs _2)
    {
        ProtocolSetup.UnregisterProtocol();
    }
}
