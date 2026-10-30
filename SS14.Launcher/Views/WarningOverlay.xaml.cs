using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SS14.Launcher.Views;

public partial class WarningOverlay : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<WarningOverlay, string?>(nameof(Title));

    public static readonly StyledProperty<IBrush?> TitleForegroundProperty =
        AvaloniaProperty.Register<WarningOverlay, IBrush?>(nameof(TitleForeground));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<WarningOverlay, string?>(nameof(Description));

    public static readonly StyledProperty<object?> ButtonTextProperty =
        AvaloniaProperty.Register<WarningOverlay, object?>(nameof(ButtonText));

    public static readonly StyledProperty<bool> ButtonEnabledProperty =
        AvaloniaProperty.Register<WarningOverlay, bool>(nameof(ButtonEnabled), true);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<WarningOverlay, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> SecondaryButtonTextProperty =
        AvaloniaProperty.Register<WarningOverlay, object?>(nameof(SecondaryButtonText));

    public static readonly StyledProperty<ICommand?> SecondaryCommandProperty =
        AvaloniaProperty.Register<WarningOverlay, ICommand?>(nameof(SecondaryCommand));

    public object? SecondaryButtonText
    {
        get => GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    public ICommand? SecondaryCommand
    {
        get => GetValue(SecondaryCommandProperty);
        set => SetValue(SecondaryCommandProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IBrush? TitleForeground
    {
        get => GetValue(TitleForegroundProperty);
        set => SetValue(TitleForegroundProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? ButtonText
    {
        get => GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public bool ButtonEnabled
    {
        get => GetValue(ButtonEnabledProperty);
        set => SetValue(ButtonEnabledProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    static WarningOverlay()
    {
        TitleForegroundProperty.Changed.AddClassHandler<WarningOverlay>((o, e) =>
        {
            if (e.NewValue is IBrush brush)
                o.TitleText.Foreground = brush;
            else
                o.TitleText.ClearValue(TextBlock.ForegroundProperty);
        });
    }

    public WarningOverlay()
    {
        InitializeComponent();
    }
}
