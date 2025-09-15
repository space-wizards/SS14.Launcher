using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Splat;
using SS14.Launcher.Utility;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SS14.Launcher.Api;
using SS14.Launcher.Models.Data;
using static SS14.Launcher.Api.HubApi;

namespace SS14.Launcher.Models.ServerStatus;

/// <summary>
///     Caches the Hub's server list.
/// </summary>
public sealed class ServerListCache : ReactiveObject, IServerSource
{
    private readonly HubApi _hubApi;
    private readonly DataManager _dataManager;

    private CancellationTokenSource? _refreshCancel;

    public ObservableCollection<ServerStatusData> AllServers => _allServers;
    private readonly ServerListCollection _allServers = new();

    [Reactive]
    public RefreshListStatus Status { get; private set; } = RefreshListStatus.NotUpdated;

    public ServerListCache()
    {
        _hubApi = Locator.Current.GetRequiredService<HubApi>();
        _dataManager = Locator.Current.GetRequiredService<DataManager>();
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
            var entries = new HashSet<HubServerListEntry>();
            var requests = new List<(Task<ServerListEntry[]> Request, Uri Hub)>();
            var allSucceeded = true;

            // Queue requests
            foreach (var hub in ConfigConstants.DefaultHubUrls)
            {
                requests.Add((_hubApi.GetServers(hub, cancel), new Uri(hub.Urls[0])));
            }

            foreach (var hub in _dataManager.Hubs.OrderBy(h => h.Priority))
            {
                requests.Add((_hubApi.GetServers(UrlFallbackSet.FromSingle(hub.Address), cancel), hub.Address));
            }

            // Await all requests
            try
            {
                // await Task.Delay(2000, cancel);
                await Task.WhenAll(requests.Select(t => t.Request));
            }
            catch
            {
                // Let's handle any exceptions later, when we have more context
            }

            // Process responses
            foreach (var (request, hub) in requests)
            {
                if (!request.IsCompletedSuccessfully)
                {
                    if (request.IsFaulted)
                    {
                        // request.Exception is non-null, see https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.isfaulted?view=net-7.0#remarks
                        foreach (var ex in request.Exception!.InnerExceptions)
                        {
                            Log.Warning("Request to hub {HubAddress} failed: {Message}", hub, ex.Message);
                        }
                    }
                    else if (request.IsCanceled)
                    {
                        Log.Warning("Request to hub {HubAddress} failed: canceled", hub);
                    }

                    allSucceeded = false;
                    continue;
                }

                foreach (var entry in request.Result)
                {
                    // Don't add server if it was already provided by another hub with higher priority
                    var maybeNewEntry = new HubServerListEntry(entry.Address, hub.AbsoluteUri, entry.StatusData);
                    if (!entries.Add(maybeNewEntry))
                    {
                        Log.Verbose("Not adding {Entry} from {ThisHub} because it was already provided by {PreviousHub}",
                            entry.Address,
                            hub.AbsoluteUri,
                            maybeNewEntry.HubAddress);
                    }
                }
            }

            _allServers.AddItems(entries.Select(entry =>
            {
                var statusData = new ServerStatusData(entry.Address, entry.HubAddress);
                ServerStatusCache.ApplyStatus(statusData, entry.StatusData);
                return statusData;
            }));

            if (_allServers.Count == 0)
                // We did not get any servers
                Status = RefreshListStatus.Error;
            else if (!allSucceeded)
                // Some hubs succeeded and returned data
                Status = RefreshListStatus.PartialError;
            else
                Status = RefreshListStatus.Updated;
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

public sealed record HubServerListEntry(string Address, string HubAddress, ServerApi.ServerStatus StatusData);
