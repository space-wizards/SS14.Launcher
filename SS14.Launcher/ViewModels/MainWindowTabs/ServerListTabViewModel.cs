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
    private string? _searchString;

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

    public bool ListTextVisible => _serverListCache.Status != RefreshListStatus.Updated;
    public bool SpinnerVisible => _serverListCache.Status < RefreshListStatus.Updated;

    public string ListText
    {
        get
        {
            var status = _serverListCache.Status;
            if (status == RefreshListStatus.Error)
                return "There was an error fetching the master server list.";

            if (status == RefreshListStatus.UpdatingMaster)
                return "Fetching master server list...";

            if (SearchedServers.Count == 0 && _allServers.Count != 0)
                return "No servers match your search.";

            if (status == RefreshListStatus.Updating)
                return "Discovering servers...";

            if (status == RefreshListStatus.NotUpdated)
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
