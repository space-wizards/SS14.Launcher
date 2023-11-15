using Avalonia.Controls;
using Avalonia.Threading;
using SS14.Launcher.ViewModels;

namespace SS14.Launcher.Views;

public partial class ConnectingOverlay : UserControl
{
    public ConnectingOverlay()
    {
        InitializeComponent();
        ConnectingViewModel.StartedConnecting += () => Dispatcher.UIThread.Post(() => CancelButton.Focus());
    }
}
