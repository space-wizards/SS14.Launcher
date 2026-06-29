using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class ServerEntryView : UserControl
{
    public ServerEntryView()
    {
        InitializeComponent();

        Links.LayoutUpdated += ApplyStyle;
    }

    // Sets the style for the link buttons correctly so that they look correct
    private void ApplyStyle(object? _1, EventArgs _2)
    {
        for (var i = 0; i < Links.ItemCount; i++)
        {
            if (Links.ContainerFromIndex(i) is not ContentPresenter { Child: ServerInfoLinkControl control } presenter)
                continue;

            presenter.ApplyTemplate();

            if (Links.ItemCount == 1)
                return;

            var style = i switch
            {
                0 => "OpenRight",
                _ when i == Links.ItemCount - 1 => "OpenLeft",
                _ => "OpenBoth",
            };

            control.GetLogicalChildren().OfType<Button>().FirstOrDefault()?.Classes.Add(style);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is ObservableRecipient r)
            r.IsActive = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (DataContext is ObservableRecipient r)
            r.IsActive = false;
    }
}
