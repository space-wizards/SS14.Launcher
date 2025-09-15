using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

namespace SS14.Launcher.Views;

/// <summary>
/// Animated spinner doodad
/// </summary>
/// <remarks>
/// Because of limitations in Avalonia, all usage sites MUST set IsVisible to false when the control is not visible.
/// Otherwise, there will be significant idle resource usage in the launcher.
/// </remarks>
[PseudoClasses("active")]
public sealed partial class DungSpinner : UserControl
{
    public static readonly StyledProperty<double> AnimationProgressProperty =
        AvaloniaProperty.Register<DungSpinner, double>(nameof(AnimationProgress));

    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<DungSpinner, IBrush>(nameof(Fill));

    private readonly IPen _pen = new Pen();

    static DungSpinner()
    {
        AffectsRender<DungSpinner>(AnimationProgressProperty, FillProperty);
    }

    public DungSpinner()
    {
        InitializeComponent();

        UpdatePseudoClass();
    }

    public double AnimationProgress
    {
        get => GetValue(AnimationProgressProperty);
        set => SetValue(AnimationProgressProperty, value);
    }

    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            UpdatePseudoClass();
        }
    }

    private void UpdatePseudoClass()
    {
        PseudoClasses.Set(":active", IsVisible);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Console.WriteLine($"RENDER: {IsEffectivelyVisible}");

        var centerX = Bounds.Width / 2;
        var centerY = Bounds.Height / 2;

        // Offset so that 0,0 is the center of the control.
        var offset = Matrix.CreateTranslation(centerX, centerY);

        using var translateState = context.PushTransform(offset);

        var brush = Fill;
        var progress = AnimationProgress * Math.PI * 2;

        void DrawElectron(double angle, double xScale, double yScale, double radius, double animationOffset,
            double mul = 1)
        {
            var rotation = Matrix.CreateRotation(angle);
            using var _ = context.PushTransform(rotation);

            var p = (progress + animationOffset) * mul;
            var x = Math.Sin(p) * xScale;
            var y = Math.Cos(p) * yScale;

            var ellipseGeometry = new EllipseGeometry(new Rect(x - radius, y - radius, radius * 2, radius * 2));

            context.DrawGeometry(brush, _pen, ellipseGeometry);
        }

        const double sizeElectron = 1.5;
        const double sizeNucleus = 3;
        const double pathX = 4;
        const double pathY = 10;

        DrawElectron(Math.PI * 2d / 3d, pathX, pathY, sizeElectron, 0.5);
        DrawElectron(Math.PI / 3d, pathX, pathY, sizeElectron, 0.33, -1);
        DrawElectron(0, pathX, pathY, sizeElectron, 0.0);
        DrawElectron(0, 0, 0, sizeNucleus, 0);
    }
}
