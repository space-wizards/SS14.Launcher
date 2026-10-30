using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SS14.Launcher.Localization;

namespace SS14.Launcher.Views;

public partial class AddFavoriteDialog : Window
{
    private readonly Func<string, bool> _isDuplicate;

    public AddFavoriteDialog() : this(null, null, _ => false, editing: false)
    {
    }

    /// <param name="name">The name to start with, or null/empty to use the name the server reports.</param>
    /// <param name="address">The address to start with.</param>
    /// <param name="isDuplicate">Says whether an address is already taken by another favorite.</param>
    /// <param name="editing">Whether this changes an existing favorite, instead of adding one.</param>
    public AddFavoriteDialog(string? name, string? address, Func<string, bool> isDuplicate, bool editing)
    {
        _isDuplicate = isDuplicate;

        InitializeComponent();

        var loc = LocalizationManager.Instance;
        Title = loc.GetString(editing ? "add-favorite-window-title-edit" : "add-favorite-window-title");
        SubmitButton.Content = loc.GetString(editing ? "add-favorite-window-button-save" : "add-favorite-window-button-add");

        NameBox.Text = name;
        AddressBox.Text = address;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        NameBox.Focus();
    }

    private void TrySubmit(object? _1, RoutedEventArgs _2)
    {
        Close((NameBox.Text?.Trim() ?? "", AddressBox.Text?.Trim() ?? ""));
    }

    private void UpdateSubmitValid(object? _1, TextChangedEventArgs _2)
    {
        var validAddr = DirectConnectDialog.IsAddressValid(AddressBox.Text);
        var duplicate = validAddr && _isDuplicate(AddressBox.Text!.Trim());

        // The name is optional, no name means the server's own name is shown.
        SubmitButton.IsEnabled = validAddr && !duplicate;

        TxtInvalid.IsVisible = !validAddr || duplicate;
        TxtInvalid.Text = LocalizationManager.Instance.GetString(
            duplicate ? "add-favorite-window-address-duplicate" : "add-favorite-window-address-invalid");
    }
}
