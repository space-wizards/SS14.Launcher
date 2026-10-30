using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DynamicData;
using DynamicData.Alias;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;
using SS14.Launcher.Views;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public partial class HomePageViewModel : MainWindowTabViewModel
{
    public MainWindowViewModel MainWindowViewModel { get; }
    private readonly DataManager _cfg;
    private readonly ServerStatusCache _statusCache = new ServerStatusCache();
    private readonly ServerListCache _serverListCache;

    public HomePageViewModel(MainWindowViewModel mainWindowViewModel)
    {
        MainWindowViewModel = mainWindowViewModel;
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _serverListCache = Locator.Current.GetRequiredService<ServerListCache>();
        _serverListCache.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ServerListCache.Status))
            {
                OnPropertyChanged(nameof(RefreshEnabled));
            }
        };

        _cfg.FavoriteServers
            .Connect()
            .Select(x => new ServerEntryViewModel(MainWindowViewModel, _statusCache.GetStatusFor(x.Address), x, _statusCache, _cfg) { ViewedInFavoritesPane = true })
            .OnItemAdded(a =>
            {
                if (IsSelected)
                {
                    _statusCache.InitialUpdateStatus(a.CacheData);
                }
            })
            // Removed favorites must not keep being refreshed (and queried) from the cache.
            .OnItemRemoved(a => _statusCache.Remove(a.CacheData.Address))
            .Sort(Comparer<ServerEntryViewModel>.Create((a, b) => {
                var dc = a.Favorite!.RaiseTime.CompareTo(b.Favorite!.RaiseTime);
                if (dc != 0)
                {
                    return -dc;
                }
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            }))
            .Bind(out var favorites)
            .Subscribe(_ =>
            {
                FavoritesEmpty = favorites.Count == 0;
            });

        Favorites = favorites;
    }

    public ReadOnlyObservableCollection<ServerEntryViewModel> Favorites { get; }

    [ObservableProperty] private bool _favoritesEmpty = true;

    public override string Name => LocalizationManager.Instance.GetString("tab-home-title");
    public Control? Control { get; set; }

    public bool RefreshEnabled => _serverListCache.Status != RefreshListStatus.UpdatingMaster;

    public async void DirectConnectPressed()
    {
        if (!TryGetWindow(out var window))
        {
            return;
        }

        var res = await new DirectConnectDialog().ShowDialog<string?>(window);
        if (res == null)
        {
            return;
        }

        ConnectingViewModel.StartConnect(MainWindowViewModel, res);
    }

    public async void AddFavoritePressed()
    {
        if (!TryGetWindow(out var window))
        {
            return;
        }

        var dialog = new AddFavoriteDialog(
            null,
            null,
            address => _cfg.FavoriteServers.Lookup(address).HasValue,
            editing: false);

        // Null if the dialog was closed without adding anything.
        if (await dialog.ShowDialog<(string name, string address)?>(window) is not { } result)
        {
            return;
        }

        try
        {
            // No name means the favorite shows the name the server reports.
            _cfg.AddFavoriteServer(new FavoriteServer(string.IsNullOrEmpty(result.name) ? null : result.name, result.address));
            _cfg.CommitConfig();
        }
        catch (ArgumentException)
        {
            // The dialog doesn't allow an address that is already a favorite, so this should not happen.
        }
    }

    private bool TryGetWindow([NotNullWhen(true)] out Window? window)
    {
        window = Control?.GetVisualRoot() as Window;
        return window != null;
    }

    public void RefreshPressed()
    {
        if (!RefreshEnabled)
            return;

        _statusCache.Refresh();
        _serverListCache.RequestRefresh();
    }

    public override void Selected()
    {
        foreach (var favorite in Favorites)
        {
            _statusCache.InitialUpdateStatus(favorite.CacheData);
        }
        _serverListCache.RequestInitialUpdate();
    }
}
