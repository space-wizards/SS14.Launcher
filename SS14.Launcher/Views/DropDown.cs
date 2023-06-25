using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace SS14.Launcher.Views;

public sealed class DropDown : TemplatedControl
{
    public static readonly DirectProperty<DropDown, bool> IsDropDownOpenProperty =
        AvaloniaProperty.RegisterDirect<DropDown, bool>(nameof(IsDropDownOpen), down => down.IsDropDownOpen,
            (down, b) => down.IsDropDownOpen = b);

    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<DropDown>();

    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        ContentControl.ContentTemplateProperty.AddOwner<DropDown>();

    public static readonly StyledProperty<object> HeaderContentProperty =
        AvaloniaProperty.Register<DropDown, object>(nameof(HeaderContent));

    public static readonly StyledProperty<IDataTemplate> HeaderContentTemplateProperty =
        AvaloniaProperty.Register<DropDown, IDataTemplate>(nameof(HeaderContentTemplate));

    private bool _isDropDownOpen;
    private Popup? _popup;

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

    public object HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public IDataTemplate HeaderContentTemplate
    {
        get => GetValue(HeaderContentTemplateProperty);
        set => SetValue(HeaderContentTemplateProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => _isDropDownOpen;
        set
        {
            SetAndRaise(IsDropDownOpenProperty, ref _isDropDownOpen, value);
            UpdatePseudoClasses();
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":pressed", IsDropDownOpen);
    }


    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.Handled)
        {
            if (e.Source != null && _popup?.IsInsidePopup((Visual) e.Source) == false)
            {
                IsDropDownOpen ^= true;
                e.Handled = true;
            }
        }

        base.OnPointerPressed(e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _popup = e.NameScope.Get<Popup>("PART_Popup");

        base.OnApplyTemplate(e);
    }
}
