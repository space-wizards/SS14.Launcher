using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Splat;
using SS14.Launcher.Models;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Api;

public sealed class HubApi
{
    private readonly HttpClient _http;
    private readonly DataManager _dataManager = Locator.Current.GetRequiredService<DataManager>();

    public HubApi(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Get a list of servers on all hubs. Returns the list of servers as well as a boolean stating whether all fetches
    /// succeeded.
    /// </summary>
    public async Task<(bool AllSucceeded, HashSet<HubServerListEntry> Entries)> GetServers(CancellationToken cancel)
    {
        var entries = new HashSet<HubServerListEntry>();
        var requests = new List<(Task<ServerListEntry[]?> Request, Hub Hub)>();
        var allSucceeded = true;

        // Queue requests
        foreach (var hub in _dataManager.Hubs)
        {
            // Sanity check, this should be enforced with code
            if (!hub.Address.AbsoluteUri.EndsWith('/'))
                throw new Exception("URI doesn't have trailing slash");

            requests.Add((_http.GetFromJsonAsync<ServerListEntry[]>(new Uri(hub.Address, "api/servers"), cancel), hub));
        }

        // Await all requests
        var tasks = Task.WhenAll(requests.Select(t => t.Request));
        try
        {
            await tasks;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException)
        {
            if (tasks.Exception?.InnerExceptions is { } inner)
            {
                foreach (var ex in inner)
                {
                    Log.Warning("Failed fetching servers from a hub: {Message}", ex.Message);
                }
            }

            allSucceeded = false;
        }

        // Process responses
        foreach (var (request, hub) in requests.OrderBy(x => x.Hub.Priority))
        {
            if (request.IsFaulted)
            {
                Log.Warning("Request to hub {HubAddress} failed", hub.Address);
                continue;
            }

            if (request.Result is not { } response)
            {
                Log.Warning("Response of hub {HubAddress} was null", hub.Address);
                allSucceeded = false;
                continue;
            }

            foreach (var entry in response)
            {
                if (!entries.Add(new HubServerListEntry(entry.Address, hub.Address.AbsoluteUri, entry.StatusData)))
                {
                    Log.Debug("Not adding duplicate server {EntryAddress}", entry.Address);
                }
            }
        }

        return (allSucceeded, entries);
    }

    public async Task<ServerInfo> GetServerInfo(string serverAddress, string hubAddress, CancellationToken cancel)
    {
        var url = $"{hubAddress}api/servers/info?url={Uri.EscapeDataString(serverAddress)}";
        return await _http.GetFromJsonAsync<ServerInfo>(url, cancel) ?? throw new InvalidDataException();
    }

    public sealed record ServerListEntry(string Address, ServerApi.ServerStatus StatusData);

    public sealed record HubServerListEntry(string Address, string HubAddress, ServerApi.ServerStatus StatusData);
}

