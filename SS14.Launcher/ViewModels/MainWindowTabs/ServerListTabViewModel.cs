using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using Splat;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class ServerListTabViewModel : MainWindowTabViewModel
{
    private readonly MainWindowViewModel _windowVm;
    private readonly HttpClient _http;
    private readonly ServerListCache _serverListCache;

    public ObservableCollection<ServerEntryViewModel> SearchedServers { get; } = new();

    private List<ServerEntryViewModel> _allServers => _serverListCache.AllServers.Select(
        x => new ServerEntryViewModel(_windowVm, x.Data) { FallbackName = x.FallbackName ?? "" }
    ).ToList();
    private RefreshListStatus _status = RefreshListStatus.NotUpdated;
    private string? _searchString;

    private RefreshListStatus Status
    {
        get => _status;
        set
        {
            this.RaiseAndSetIfChanged(ref _status, value);
            this.RaisePropertyChanged(nameof(ListText));
            this.RaisePropertyChanged(nameof(ListTextVisible));
            this.RaisePropertyChanged(nameof(SpinnerVisible));
        }
    }

    public override string Name => "Servers";

    [Reactive]
    public string? SearchString
    {
        get => _searchString;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchString, value);
            UpdateSearchedList();
        }
    }

    public bool ListTextVisible => Status != RefreshListStatus.Updated;
    public bool SpinnerVisible => Status < RefreshListStatus.Updated;

    public string ListText
    {
        get
        {
            if (Status == RefreshListStatus.Error)
                return "There was an error fetching the master server list.";

            if (Status == RefreshListStatus.UpdatingMaster)
                return "Fetching master server list...";

            if (SearchedServers.Count == 0 && _allServers.Count != 0)
                return "No servers match your search.";

            if (Status == RefreshListStatus.Updating)
                return "Discovering servers...";

            if (Status == RefreshListStatus.NotUpdated)
                return "";

            if (_allServers.Count == 0)
                return "There's no public servers, apparently?";

            return "";
        }
    }

    public ServerListTabViewModel(MainWindowViewModel windowVm)
    {
        _windowVm = windowVm;
        _http = Locator.Current.GetRequiredService<HttpClient>();
        _serverListCache = Locator.Current.GetRequiredService<ServerListCache>();
        _serverListCache.AllServers.CollectionChanged += (_, _) => UpdateSearchedList();
        _serverListCache.StatusUpdated += () => Status = _serverListCache.Status;
    }

    public override void Selected()
    {
        _serverListCache.RequestInitialUpdate();
    }

    public void RefreshPressed()
    {
        _serverListCache.RequestRefresh();
    }

    private void UpdateSearchedList()
    {
        var sortList = new List<ServerEntryViewModel>();

        foreach (var server in _allServers)
        {
            if (DoesSearchMatch(server))
                sortList.Add(server);
        }

        sortList.Sort(Comparer<ServerEntryViewModel>.Create((a, b) =>
            b.CacheData.PlayerCount.CompareTo(a.CacheData.PlayerCount)));

        SearchedServers.Clear();
        foreach (var server in sortList)
        {
            SearchedServers.Add(server);
        }
    }

    private bool DoesSearchMatch(ServerEntryViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(SearchString))
            return true;

        return vm.CacheData.Name != null &&
               vm.CacheData.Name.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase);
    }
}
