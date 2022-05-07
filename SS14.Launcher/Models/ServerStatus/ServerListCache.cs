using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using Splat;
using SS14.Launcher.Utility;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace SS14.Launcher.Models.ServerStatus;

/// <summary>
///     Caches the Hub's server list.
/// </summary>
public sealed class ServerListCache : ReactiveObject
{
    private readonly HttpClient _http;

    private CancellationTokenSource? _refreshCancel;

    public readonly ObservableCollection<ServerStatusDataWithFallbackName> AllServers = new();

    [Reactive] public RefreshListStatus Status { get; private set; } = RefreshListStatus.NotUpdated;
    public event Action? StatusUpdated;

    public ServerListCache()
    {
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    /// <summary>
    /// This function requests the initial update from the server if one hasn't already been requested.
    /// </summary>
    public void RequestInitialUpdate()
    {
        if (Status == RefreshListStatus.NotUpdated)
        {
            _refreshCancel?.Cancel();
            _refreshCancel = new CancellationTokenSource();
            RefreshServerList(_refreshCancel.Token);
        }
    }

    /// <summary>
    /// This function performs a refresh.
    /// </summary>
    public void RequestRefresh()
    {
        _refreshCancel?.Cancel();
        AllServers.Clear();
        _refreshCancel = new CancellationTokenSource();
        RefreshServerList(_refreshCancel.Token);
    }

    public async void RefreshServerList(CancellationToken cancel)
    {
        AllServers.Clear();
        Status = RefreshListStatus.UpdatingMaster;
        StatusUpdated?.Invoke();

        try
        {
            using var response = await _http.GetAsync(ConfigConstants.HubUrl + "api/servers", cancel);

            response.EnsureSuccessStatusCode();

            var entries = await response.Content.AsJson<ServerListEntry[]>();

            Status = RefreshListStatus.Updating;
            StatusUpdated?.Invoke();

            await Parallel.ForEachAsync(entries, new ParallelOptions
            {
                MaxDegreeOfParallelism = 20,
                CancellationToken = cancel
            }, async (entry, token) =>
            {
                var status = new ServerStatusData(entry.Address);
                await ServerStatusCache.UpdateStatusFor(status, _http, token);

                if (status.Status == ServerStatusCode.Offline)
                    return;

                Dispatcher.UIThread.Post(() =>
                {
                    if (!cancel.IsCancellationRequested)
                    {
                        // Log.Information("{Name}: {Address}", status.Name, status.Address);
                        AllServers.Add(new ServerStatusDataWithFallbackName(status, entry.Name));
                    }
                });
            });

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
        StatusUpdated?.Invoke();
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
    /// An error occured.
    /// </summary>
    Error
}

public sealed class ServerListEntry
{
    public string Address { get; set; } = default!;
    public string Name { get; set; } = default!;
}

