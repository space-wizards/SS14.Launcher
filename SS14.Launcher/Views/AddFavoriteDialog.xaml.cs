using System;
using Avalonia.Controls;
using ReactiveUI;

namespace SS14.Launcher.Views;

public partial class AddFavoriteDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBox _addressBox;

    public AddFavoriteDialog()
    {
        InitializeComponent();

        _nameBox = NameBox;
        _addressBox = AddressBox;

        SubmitButton.Command = ReactiveCommand.Create(TrySubmit);

        this.WhenAnyValue(x => x._nameBox.Text)
            .Subscribe(_ => UpdateSubmitValid());

        this.WhenAnyValue(x => x._addressBox.Text)
            .Subscribe(_ => UpdateSubmitValid());
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _nameBox.Focus();
    }

    private void TrySubmit()
    {
        Close((_nameBox.Text.Trim(), _addressBox.Text.Trim()));
    }

    private void UpdateSubmitValid()
    {
        var validAddr = DirectConnectDialog.IsAddressValid(_addressBox.Text);
        var valid = validAddr && !string.IsNullOrEmpty(_nameBox.Text);

        SubmitButton.IsEnabled = valid;
        TxtInvalid.IsVisible = !validAddr;
    }
}
