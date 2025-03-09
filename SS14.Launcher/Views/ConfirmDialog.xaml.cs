using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;

namespace SS14.Launcher.Views;

public partial class ConfirmDialog : Window
{
    public string? DialogContent
    {
        get => Content.Text;
        set => Content.Text = value;
    }

    public string? ConfirmButtonText
    {
        get => ConfirmButton.Content as string;
        set => ConfirmButton.Content = value;
    }

    public string? CancelButtonText
    {
        get => CancelButton.Content as string;
        set => CancelButton.Content = value;
    }

    public ConfirmDialog()
    {
        InitializeComponent();

        ConfirmButton.Command = ReactiveCommand.Create(() => Close(true));
        CancelButton.Command = ReactiveCommand.Create(() => Close(false));
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
