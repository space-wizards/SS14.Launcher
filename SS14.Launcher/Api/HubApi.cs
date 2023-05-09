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
using SS14.Launcher.Models.ServerStatus;
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
    public async Task<(bool AllSucceeded, List<HubServerListEntry> Entries)> GetServers(CancellationToken cancel)
    {
        var entries = new List<HubServerListEntry>();
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
            try
            {
                var response = await request;

                if (response == null)
                {
                    allSucceeded = false;
                    continue;
                }

                // Remove duplicate servers
                // This only removes
                var deduped = response.Where(e => !entries
                    .Select(x => x.Address)
                    .Contains(e.Address)
                    ).ToArray();

                foreach (var dupe in response.Except(deduped))
                {
                    Log.Debug("Removed duplicate server {Address}", dupe.Address);
                }

                entries.AddRange(deduped.Select(s =>
                    new HubServerListEntry(s.Address, hub.Address.AbsoluteUri, s.StatusData)));
            }
            catch
            {
                // Handled by WhenAll
            }
        }

        return (allSucceeded, entries);
    }


    public async Task<ServerInfo> GetServerInfo(ServerStatusData statusData, CancellationToken cancel)
    {
        if (statusData.HubAddress == null)
        {
            Log.Error("Tried to get server info for hubbed server {Name} without HubAddress set", statusData.Name);
        }

        var url = $"{statusData.HubAddress}api/servers/info?url={Uri.EscapeDataString(statusData.Address)}";
        return await _http.GetFromJsonAsync<ServerInfo>(url, cancel) ?? throw new InvalidDataException();
    }

    public sealed record ServerListEntry(string Address, ServerApi.ServerStatus StatusData);

    public sealed record HubServerListEntry(string Address, string HubAddress, ServerApi.ServerStatus StatusData);
}

