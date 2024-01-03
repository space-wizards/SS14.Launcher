using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Serilog;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class ServerEntryView : UserControl
{
    private readonly IImage _starIcon;
    private readonly IImage _starOutlineIcon;

    public ServerEntryView()
    {
        InitializeComponent();

        Links.LayoutUpdated += UpdateLinkButtons;
        FavoriteButtonIconLabel.LayoutUpdated += UpdateFavoriteButton;

        if (Application.Current?.FindResource("ButtonIcon-star") as IImage is not { } starIcon ||
            Application.Current.FindResource("ButtonIcon-star-outline") as IImage is not { } starOutlineIcon)
        {
            throw new Exception("Failed to load favorite icons");
        }

        _starIcon = starIcon;
        _starOutlineIcon = starOutlineIcon;
    }

    private void UpdateFavoriteButton(object? _1, EventArgs _2)
    {
        if (DataContext as ServerEntryViewModel is not { } context)
        {
            Log.Error($"Failed to get DataContext in {nameof(UpdateFavoriteButton)}");
            return;
        }

        if (context.ViewedInFavoritesPane)
            FavoriteButton.Classes.Add("OpenRight");
        else
            FavoriteButton.Classes.Remove("OpenRight");

        FavoriteButtonIconLabel.Icon = context.IsFavorite ? _starIcon : _starOutlineIcon;
    }

    // Sets the style for the link buttons correctly so that they look correct
    private void UpdateLinkButtons(object? _1, EventArgs _2)
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
