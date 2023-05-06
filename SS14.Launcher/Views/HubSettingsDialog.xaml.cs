using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SS14.Launcher.ViewModels;

namespace SS14.Launcher.Views;

public partial class HubSettingsDialog : Window
{
    private readonly HubSettingsViewModel _viewModel;

    public HubSettingsDialog()
    {
        InitializeComponent();

#if DEBUG
        this.AttachDevTools();
#endif

        _viewModel = (DataContext as HubSettingsViewModel)!; // Should have been set in XAML
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _viewModel.Populate();
    }

    private void Done(object? sender, RoutedEventArgs args)
    {
        _viewModel.Save();
        Close();
    }

    private void Cancel(object? sender, RoutedEventArgs args) => Close();

    private void UpdateSubmitValid()
    {
        /*var validAddr = DirectConnectDialog.IsAddressValid(_addressBox.Text);
        var valid = validAddr && !string.IsNullOrEmpty(_nameBox.Text);

        SubmitButton.IsEnabled = valid;*/
        //TxtInvalid.IsVisible = !validAddr;
    }
}
