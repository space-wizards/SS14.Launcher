using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using SS14.Launcher.Localization;

namespace SS14.Launcher.Controls;

public class TimerTextBlock : TemplatedControl
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    public static readonly DirectProperty<TimerTextBlock, DateTime?> ValueProperty =
        AvaloniaProperty.RegisterDirect<TimerTextBlock, DateTime?>(
            nameof(Value),
            o => o.Value,
            (o, v) => o.Value = v
        );

    public static readonly DirectProperty<TimerTextBlock, string> TextProperty =
        AvaloniaProperty.RegisterDirect<TimerTextBlock, string>(
            nameof(Text),
            o => o.Text,
            (o, v) => o.Text = v
        );

    private DateTime? _value;

    public DateTime? Value
    {
        get => _value;
        set => SetAndRaise(ValueProperty, ref _value, value);
    }

    private string _text = "";

    public string Text
    {
        get => _text;
        set => SetAndRaise(TextProperty, ref _text, value);
    }

    private readonly DispatcherTimer _timer = new DispatcherTimer();

    public TimerTextBlock()
    {
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            this.UpdateText();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
    }

    private void UpdateText()
    {
        this.Text = Value is { } dt ? GetTimeStringSince(dt) : "";
    }

    private string GetTimeStringSince(DateTime dateTime)
    {
        var ts = DateTime.Now.ToUniversalTime().Subtract(dateTime);
        return _loc.GetString("server-entry-round-time", ("hours", ts.Hours),
            ("mins", ts.Minutes.ToString().PadLeft(2, '0')));
    }
}
