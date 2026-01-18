using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SS14.Launcher.Views;

public partial class DirectConnectDialog : Window
{
    public DirectConnectDialog()
    {
        InitializeComponent();

        AddressBox.TextChanged += (_, _) =>
        {
            var valid = IsAddressValid(AddressBox.Text);
            InvalidLabel.IsVisible = !valid;
            SubmitButton.IsEnabled = valid;
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        AddressBox.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
        }

        base.OnKeyDown(e);
    }

    private void TrySubmit(object? sender, RoutedEventArgs routedEventArgs)
    {
        if (!IsAddressValid(AddressBox.Text))
        {
            return;
        }

        Close(AddressBox.Text.Trim());
    }

    internal static bool IsAddressValid([NotNullWhen(true)] string? address)
    {
        return !string.IsNullOrWhiteSpace(address) && UriHelper.TryParseSs14Uri(address, out _);
    }
}
