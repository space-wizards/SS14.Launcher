using System;
using Avalonia;
using Avalonia.Controls;

namespace SS14.Launcher.Views;

public sealed class RowSideAlignedPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
            return base.MeasureOverride(availableSize);

        var left = Children[0];
        var right = Children[1];

        left.Measure(availableSize);
        right.Measure(availableSize);

        var leftSize = left.DesiredSize;
        var rightSize = right.DesiredSize;

        if (leftSize.Width + rightSize.Width <= availableSize.Width)
        {
            // They both fit on one row, easy.
            return new Size(leftSize.Width + rightSize.Width, Math.Max(leftSize.Height, rightSize.Height));
        }
        else
        {
            // They don't fit on the same row, make two rows.
            return new Size(Math.Max(leftSize.Width, rightSize.Width), leftSize.Height + rightSize.Height);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
            return base.MeasureOverride(finalSize);

        var left = Children[0];
        var right = Children[1];

        var leftSize = left.DesiredSize;
        var rightSize = right.DesiredSize;

        if (leftSize.Width + rightSize.Width <= finalSize.Width)
        {
            // They both fit on one row, easy.
            left.Arrange(new Rect(0, 0, leftSize.Width, finalSize.Height));
            right.Arrange(new Rect(finalSize.Width - rightSize.Width, 0, rightSize.Width, finalSize.Height));

            return finalSize;
        }
        else
        {
            // They don't fit on the same row, make two rows.
            left.Arrange(new Rect(0, 0, leftSize.Width, leftSize.Height));
            right.Arrange(new Rect(finalSize.Width - rightSize.Width, leftSize.Height, rightSize.Width, finalSize.Height - leftSize.Height));

            return finalSize;
        }
    }
}
