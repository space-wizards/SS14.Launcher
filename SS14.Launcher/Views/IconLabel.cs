using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Metadata;

namespace SS14.Launcher.Views;

public class IconLabel : TemplatedControl
{
    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<IconLabel>();

    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        ContentControl.ContentTemplateProperty.AddOwner<IconLabel>();

    public static readonly StyledProperty<IImage> IconProperty =
        AvaloniaProperty.Register<IconLabel, IImage>(nameof(Icon));

    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public IImage Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
