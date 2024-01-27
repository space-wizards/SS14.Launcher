using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;

namespace SS14.Launcher.Views;

public partial class SentryFeedbackWindow : Window
{
    public int MaxFeedbackLength { get; set; } = 500;

    public bool DisplayWarningIcon
    {
        get => WarningIcon.IsVisible;
        set => WarningIcon.IsVisible = value;
    }

    public string? DialogText
    {
        get => Content.Text;
        set => Content.Text = value;
    }

    public string? SendButtonText
    {
        get => SendButton.Content as string;
        set => SendButton.Content = value;
    }

    public SentryFeedbackWindow()
    {
        InitializeComponent();

        SendButton.Command = ReactiveCommand.Create(() => Close(FeedbackField.Text));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
        }

        base.OnKeyDown(e);
    }

    internal static bool IsAddressValid(string address)
    {
        return !string.IsNullOrWhiteSpace(address) && UriHelper.TryParseSs14Uri(address, out _);
    }

    private void FeedbackTextChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (FeedbackField == null || ValidationText == null)
            return;

        var text = FeedbackField.Text;
        if (text == null || text.Length < MaxFeedbackLength)
        {
            FeedbackField.Classes.Remove("Invalid");
            ValidationText.Text = null;
            return;
        }

        FeedbackField.Text = text[..MaxFeedbackLength];
        ValidationText.Text = $"Feedback can't be longer than {MaxFeedbackLength} characters";
        FeedbackField.Classes.Add("Invalid");
    }
}
