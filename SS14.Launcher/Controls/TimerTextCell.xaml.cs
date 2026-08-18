using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using SS14.Launcher.Localization;

namespace SS14.Launcher.Controls;

public class TimerTextCell : TemplatedControl
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    public static readonly DirectProperty<TimerTextCell, DateTime?> ValueProperty =
        AvaloniaProperty.RegisterDirect<TimerTextCell, DateTime?>(
            nameof(Value),
            o => o.Value,
            (o, v) => o.Value = v
        );

    public static readonly DirectProperty<TimerTextCell, string> TextProperty =
        AvaloniaProperty.RegisterDirect<TimerTextCell, string>(
            nameof(Text),
            o => o.Text,
            (o, v) => o.Text = v
        );

    private DateTime? _value;
    private bool _attached;

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

    private IDisposable? _timer;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            UpdateText();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        UpdateText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _timer?.Dispose();
        _timer = null;
    }

    // Trigger an update when the visible timer will roll over to the next minute
    private void StartTimer()
    {
        _timer?.Dispose();

        // Only start a new timer if we have a DateTime
        // and we’re on the visual tree.
        if (_attached && Value is { } dt)
        {
            _timer = DispatcherTimer.RunOnce(UpdateText, GetDelayUntilNextMinute(dt));
        }
    }

    private void UpdateText()
    {
        this.Text = Value is { } dt ? GetTimeStringSince(dt) : "";
        StartTimer();
    }

    private static TimeSpan GetDelayUntilNextMinute(DateTime dateTime)
    {
        var elapsed = DateTime.UtcNow.Subtract(dateTime);
        if (elapsed < TimeSpan.Zero)
            return TimeSpan.FromSeconds(1);

        var secondsIntoMinute = (int)Math.Floor(elapsed.TotalSeconds) % 60;
        var secondsUntilNextMinute = 60 - secondsIntoMinute;
        return TimeSpan.FromSeconds(secondsUntilNextMinute);
    }

    private string GetTimeStringSince(DateTime dateTime)
    {
        var ts = DateTime.UtcNow.Subtract(dateTime);
        return _loc.GetString("server-entry-round-time", ("hours", Math.Floor(ts.TotalHours)),
            ("mins", ts.Minutes.ToString().PadLeft(2, '0')));
    }
}
