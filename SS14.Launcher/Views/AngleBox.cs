using System;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Visuals.Platform;

namespace SS14.Launcher.Views;

public class AngleBox : Shape
{
    public static readonly StyledProperty<double> CornerSizeProperty
        = AvaloniaProperty.Register<AngleBox, double>("CornerSize");

    public static readonly StyledProperty<AngleBoxSideStyle> SideStyleProperty
        = AvaloniaProperty.Register<AngleBox, AngleBoxSideStyle>("SideStyle");

    static AngleBox()
    {
        AffectsMeasure<AngleBox>(CornerSizeProperty, SideStyleProperty);
        AffectsGeometry<AngleBox>(BoundsProperty, CornerSizeProperty, SideStyleProperty);
    }

    public double CornerSize
    {
        get => GetValue(CornerSizeProperty);
        set => SetValue(CornerSizeProperty, value);
    }

    public AngleBoxSideStyle SideStyle
    {
        get => GetValue(SideStyleProperty);
        set => SetValue(SideStyleProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var style = SideStyle;
        var c = CornerSize;

        var size = new Size();
        if ((style & AngleBoxSideStyle.OpenRight) == 0)
            size += new Size(c, c);

        if ((style & AngleBoxSideStyle.OpenLeft) == 0)
            size += new Size(c, c);

        return size;
    }

    protected override Geometry CreateDefiningGeometry()
    {
        var style = SideStyle;

        var geometry = new PathGeometry();
        var context = new PathGeometryContext(geometry);

        var c = CornerSize;
        var b = Bounds;

        context.BeginFigure(new(0, 0), isFilled: true);
        if ((style & AngleBoxSideStyle.OpenRight) != 0)
        {
            context.LineTo(new(b.Width, 0));
        }
        else
        {
            context.LineTo(new(b.Width-c, 0));
            context.LineTo(new(b.Width, c));
        }

        context.LineTo(new(b.Width, b.Height));

        if ((style & AngleBoxSideStyle.OpenLeft) != 0)
        {
            context.LineTo(new(0, b.Height));
        }
        else
        {
            context.LineTo(new(c, b.Height));
            context.LineTo(new(0, b.Height-c));
        }

        context.EndFigure(isClosed: true);

        return geometry;
    }
}

[Flags]
public enum AngleBoxSideStyle
{
    Full = 0,
    OpenLeft = 1 << 0,
    OpenRight = 1 << 1,
    OpenBoth = OpenLeft | OpenRight
}
