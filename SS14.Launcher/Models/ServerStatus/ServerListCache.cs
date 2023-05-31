using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using Serilog;
using Splat;
using SS14.Launcher.Utility;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SS14.Launcher.Api;

namespace SS14.Launcher.Models.ServerStatus;

/// <summary>
///     Caches the Hub's server list.
/// </summary>
public sealed class ServerListCache : ReactiveObject, IServerSource
{
    private readonly HubApi _hubApi;

    private CancellationTokenSource? _refreshCancel;

    public ObservableCollection<ServerStatusData> AllServers => _allServers;
    private readonly ServerListCollection _allServers = new();

    [Reactive]
    public RefreshListStatus Status { get; private set; } = RefreshListStatus.NotUpdated;

    public ServerListCache()
    {
        _hubApi = Locator.Current.GetRequiredService<HubApi>();
    }

    /// <summary>
    /// This function requests the initial update from the server if one hasn't already been requested.
    /// </summary>
    public void RequestInitialUpdate()
    {
        if (Status == RefreshListStatus.NotUpdated)
        {
            RequestRefresh();
        }
    }

    /// <summary>
    /// This function performs a refresh.
    /// </summary>
    public void RequestRefresh()
    {
        _refreshCancel?.Cancel();
        _allServers.Clear();
        _refreshCancel = new CancellationTokenSource(10000);
        RefreshServerList(_refreshCancel.Token);
    }

    public async void RefreshServerList(CancellationToken cancel)
    {
        _allServers.Clear();
        Status = RefreshListStatus.UpdatingMaster;

        try
        {
            var (allSucceeded, entries) = await _hubApi.GetServers(cancel);

            Status = RefreshListStatus.Updating;

            _allServers.AddItems(entries.Select(entry =>
            {
                var statusData = new ServerStatusData(entry.Address, entry.HubAddress);
                ServerStatusCache.ApplyStatus(statusData, entry.StatusData);
                return statusData;
            }));

            Status = allSucceeded ? RefreshListStatus.Updated : RefreshListStatus.PartialError;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to fetch server list due to exception");
            Status = RefreshListStatus.Error;
        }
    }

    void IServerSource.UpdateInfoFor(ServerStatusData statusData)
    {
        if (statusData.HubAddress == null)
        {
            Log.Error("Tried to get server info for hubbed server {Name} without HubAddress set", statusData.Name);
            return;
        }

        ServerStatusCache.UpdateInfoForCore(
            statusData,
            async token => await _hubApi.GetServerInfo(statusData.Address, statusData.HubAddress, token));
    }

    private sealed class ServerListCollection : ObservableCollection<ServerStatusData>
    {
        public void AddItems(IEnumerable<ServerStatusData> items)
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}

public class ServerStatusDataWithFallbackName
{
    public readonly ServerStatusData Data;
    public readonly string? FallbackName;

    public ServerStatusDataWithFallbackName(ServerStatusData data, string? name)
    {
        Data = data;
        FallbackName = name;
    }
}

public enum RefreshListStatus
{
    /// <summary>
    /// Hasn't started updating yet?
    /// </summary>
    NotUpdated,

    /// <summary>
    /// Fetching master server list.
    /// </summary>
    UpdatingMaster,

    /// <summary>
    /// Fetched master server list and currently fetching information from master servers.
    /// </summary>
    Updating,

    /// <summary>
    /// Fetched information from ALL servers from the hub.
    /// </summary>
    Updated,

    /// <summary>
    /// A connection error occured when fetching from at least one hub.
    /// </summary>
    PartialError,

    /// <summary>
    /// An error occured.
    /// </summary>
    Error,
}

