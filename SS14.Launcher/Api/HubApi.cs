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
using SS14.Launcher.Models;
using SS14.Launcher.Models.ServerStatus;

namespace SS14.Launcher.Api;

public sealed class HubApi
{
    private readonly HttpClient _http;

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
        var allSucceeded = true;

        foreach (var url in ConfigConstants.DefaultHubUrls)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<ServerListEntry[]>(url + "api/servers", cancel)
                               ?? throw new JsonException("Server list is null!");

                entries.AddRange(response.Select(s => new HubServerListEntry(s.Address, url, s.StatusData)));
            }
            catch (Exception e)
            {
                // Only continue if this specific hub server is acting weird, otherwise throw
                if (e is not (HttpRequestException or JsonException)) throw;

                allSucceeded = false;
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

