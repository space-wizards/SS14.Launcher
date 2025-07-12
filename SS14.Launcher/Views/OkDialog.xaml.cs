using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;

namespace SS14.Launcher.Views;

public partial class OkDialog : Window
{
    public string? DialogContent
    {
        get => Content.Text;
        set => Content.Text = value;
    }

    public string? ButtonText
    {
        get => OkButton.Content as string;
        set => OkButton.Content = value;
    }

    public OkDialog()
    {
        InitializeComponent();

        OkButton.Command = ReactiveCommand.Create(Close);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
        }

        base.OnKeyDown(e);
    }
}
