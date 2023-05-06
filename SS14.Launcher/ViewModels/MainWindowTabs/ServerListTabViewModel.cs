using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class ServerListTabViewModel : MainWindowTabViewModel
{
    private readonly MainWindowViewModel _windowVm;
    private readonly ServerListCache _serverListCache;

    public ObservableCollection<ServerEntryViewModel> SearchedServers { get; } = new();

    private string? _searchString;

    public override string Name => "Servers";

    public string? SearchString
    {
        get => _searchString;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchString, value);
            UpdateSearchedList();
        }
    }

    public bool ListTextVisible => _serverListCache.Status != RefreshListStatus.Updated;
    public bool SpinnerVisible => _serverListCache.Status < RefreshListStatus.Updated;

    public string ListText
    {
        get
        {
            var status = _serverListCache.Status;
            switch (status)
            {
                case RefreshListStatus.Error:
                    return "There was an error fetching the master server lists.";
                case RefreshListStatus.PartialError:
                    return "Failed to fetch some or all server lists. Ensure your hub configuration is correct.";
                case RefreshListStatus.UpdatingMaster:
                    return "Fetching master server list...";
                case RefreshListStatus.Updating:
                    return "Discovering servers...";
                case RefreshListStatus.NotUpdated:
                    return "";
                case RefreshListStatus.Updated:
                default:
                    if (SearchedServers.Count == 0 && _serverListCache.AllServers.Count != 0)
                        // TODO: Actually make this show up or just remove it entirely
                        return "No servers match your search or filter settings.";

                    if (_serverListCache.AllServers.Count == 0)
                        return "There are no public servers. Ensure your hub configuration is correct.";

                    return "";
            }
        }
    }

    [Reactive] public bool FiltersVisible { get; set; }

    public ServerListFiltersViewModel Filters { get; }

    public ServerListTabViewModel(MainWindowViewModel windowVm)
    {
        Filters = new ServerListFiltersViewModel(windowVm.Cfg);
        Filters.FiltersUpdated += FiltersOnFiltersUpdated;

        _windowVm = windowVm;
        _serverListCache = Locator.Current.GetRequiredService<ServerListCache>();

        _serverListCache.AllServers.CollectionChanged += ServerListUpdated;

        _serverListCache.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(ServerListCache.Status):
                    this.RaisePropertyChanged(nameof(ListText));
                    this.RaisePropertyChanged(nameof(ListTextVisible));
                    this.RaisePropertyChanged(nameof(SpinnerVisible));
                    break;
            }
        };
    }

    private void FiltersOnFiltersUpdated()
    {
        UpdateSearchedList();
    }

    public override void Selected()
    {
        _serverListCache.RequestInitialUpdate();
    }

    public void RefreshPressed()
    {
        _serverListCache.RequestRefresh();
    }

    private void ServerListUpdated(object? sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
    {
        Filters.UpdatePresentFilters(_serverListCache.AllServers);

        UpdateSearchedList();
    }

    private void UpdateSearchedList()
    {
        var sortList = new List<ServerStatusData>();

        foreach (var server in _serverListCache.AllServers)
        {
            if (!DoesSearchMatch(server))
                continue;

            sortList.Add(server);
        }

        Filters.ApplyFilters(sortList);

        sortList.Sort(ServerSortComparer.Instance);

        SearchedServers.Clear();
        foreach (var server in sortList)
        {
            var vm = new ServerEntryViewModel(_windowVm, server, _serverListCache, _windowVm.Cfg);
            SearchedServers.Add(vm);
        }
    }

    private bool DoesSearchMatch(ServerStatusData data)
    {
        if (string.IsNullOrWhiteSpace(SearchString))
            return true;

        return data.Name != null &&
               data.Name.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase);
    }

    private sealed class ServerSortComparer : NotNullComparer<ServerStatusData>
    {
        public static readonly ServerSortComparer Instance = new();

        public override int Compare(ServerStatusData x, ServerStatusData y)
        {
            // Sort by player count descending.
            var res = x.PlayerCount.CompareTo(y.PlayerCount);
            if (res != 0)
                return -res;

            // Sort by name.
            res = string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
            if (res != 0)
                return res;

            // Sort by address.
            return string.Compare(x.Address, y.Address, StringComparison.Ordinal);
        }
    }
}
