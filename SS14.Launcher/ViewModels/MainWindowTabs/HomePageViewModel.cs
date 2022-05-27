using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DynamicData;
using DynamicData.Alias;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;
using SS14.Launcher.Views;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class HomePageViewModel : MainWindowTabViewModel
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

        _cfg.FavoriteServers
            .Connect()
            .Select(x => new ServerEntryViewModel(MainWindowViewModel, _statusCache.GetStatusFor(x.Address), x) { ViewedInFavoritesPane = true })
            .OnItemAdded(a =>
            {
                if (IsSelected)
                {
                    _statusCache.InitialUpdateStatus(a.CacheData);
                }
            })
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
                ShowSuggestions = favorites.Count <= 3;
            });

        Favorites = favorites;

        _serverListCache.AllServers.CollectionChanged += (_, _) => UpdateSuggestions();
    }

    public ReadOnlyObservableCollection<ServerEntryViewModel> Favorites { get; }
    public ObservableCollection<ServerEntryViewModel> Suggestions { get; } = new();

    [Reactive] public bool FavoritesEmpty { get; private set; } = true;
    [Reactive] public bool ShowSuggestions { get; private set; } = true;

    public override string Name => "Home";
    public Control? Control { get; set; }

    public async void DirectConnectPressed()
    {
        if (!TryGetWindow(out var window))
        {
            return;
        }

        var res = await new DirectConnectDialog().ShowDialog<string>(window);
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

        var (name, address) = await new AddFavoriteDialog().ShowDialog<(string name, string address)>(window);

        try
        {
            _cfg.AddFavoriteServer(new FavoriteServer(name, address));
            _cfg.CommitConfig();
        }
        catch (ArgumentException)
        {
            // Happens if address already a favorite, so ignore.
            // TODO: Give a popup to the user?
        }
    }

    private bool TryGetWindow([MaybeNullWhen(false)] out Window? window)
    {
        window = Control?.GetVisualRoot() as Window;
        return window != null;
    }

    public void RefreshPressed()
    {
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

    public void UpdateSuggestions()
    {
        // Determine suggestions.
        // Note that we don't bother updating this when favorites change.
        Suggestions.Clear();
        var candidates = _serverListCache.AllServers.
            Where(x => x.Data.PlayerCount != 0). // Servers with players
            Where(x => !(_cfg.FavoriteServers.Lookup(x.Data.Address).HasValue)). // That are not already favorited
            ToList();
        var rng = new Random();
        // Pick candidates.
        for (var i = 0; i < 2; i++)
        {
            if (candidates.Count == 0)
            {
                break;
            }
            var p = rng.Next(candidates.Count);
            var v = candidates[p];
            candidates.RemoveAt(p);
            Suggestions.Add(new ServerEntryViewModel(MainWindowViewModel, v));
        }
    }
}
