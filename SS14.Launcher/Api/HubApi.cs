using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SS14.Launcher.Models;

namespace SS14.Launcher.Api;

public sealed class HubApi
{
    private readonly HttpClient _http;

    public HubApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<ServerListEntry[]> GetServers(Uri hubUri, CancellationToken cancel)
    {
        // Sanity check, this should be enforced with code
        if (!hubUri.AbsoluteUri.EndsWith('/'))
            throw new Exception("URI doesn't have trailing slash");

        return await _http.GetFromJsonAsync<ServerListEntry[]>(new Uri(hubUri, "api/servers"), cancel)
               ?? throw new JsonException("Server list is null!");
    }

    public async Task<ServerInfo> GetServerInfo(string serverAddress, string hubAddress, CancellationToken cancel)
    {
        var url = $"{hubAddress}api/servers/info?url={Uri.EscapeDataString(serverAddress)}";
        return await _http.GetFromJsonAsync<ServerInfo>(url, cancel) ?? throw new InvalidDataException();
    }

    public sealed record ServerListEntry(string Address, ServerApi.ServerStatus StatusData);
}

