using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Splat;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.ServerStatus;

/// <summary>
///     Caches information pulled from servers and updates it asynchronously.
/// </summary>
public sealed class ServerStatusCache
{
    // Yes this class "memory leaks" because it never frees these data objects.
    // Oh well!
    private readonly Dictionary<string, CacheReg> _cachedData = new();
    private readonly HttpClient _http;

    public ServerStatusCache()
    {
        _http = Locator.Current.GetRequiredService<HttpClient>();
    }

    /// <summary>
    ///     Gets an uninitialized status for a server address.
    ///     This does NOT start fetching the data.
    /// </summary>
    /// <param name="serverAddress">The address of the server to fetch data for.</param>
    public ServerStatusData GetStatusFor(string serverAddress)
    {
        if (_cachedData.TryGetValue(serverAddress, out var reg))
            return reg.Data;

        var data = new ServerStatusData(serverAddress);
        reg = new CacheReg(data);
        _cachedData.Add(serverAddress, reg);

        return data;
    }

    /// <summary>
    ///     Do the initial status update for a server status. This only acts once.
    /// </summary>
    public void InitialUpdateStatus(ServerStatusData data)
    {
        var reg = _cachedData[data.Address];
        if (reg.DidInitialStatusUpdate)
            return;

        UpdateStatusFor(reg);
    }

    private async void UpdateStatusFor(CacheReg reg)
    {
        reg.DidInitialStatusUpdate = true;
        await reg.Semaphore.WaitAsync();
        var cancelSource = reg.Cancellation = new CancellationTokenSource();
        var cancel = cancelSource.Token;
        try
        {
            await UpdateStatusFor(reg.Data, _http, cancel);
        }
        finally
        {
            reg.Semaphore.Release();
        }
    }

    public static async Task UpdateStatusFor(ServerStatusData data, HttpClient http, CancellationToken cancel)
    {
        try
        {
            if (!UriHelper.TryParseSs14Uri(data.Address, out var parsedAddress))
            {
                Log.Warning("Server {Server} has invalid URI {Uri}", data.Name, data.Address);
                data.Status = ServerStatusCode.Offline;
                return;
            }

            var statusAddr = UriHelper.GetServerStatusAddress(parsedAddress);
            data.Status = ServerStatusCode.FetchingStatus;

            ServerStatus status;
            try
            {
                // await Task.Delay(Random.Shared.Next(150, 5000), cancel);

                using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    linkedToken.CancelAfter(ConfigConstants.ServerStatusTimeout);

                    status = await http.GetFromJsonAsync<ServerStatus>(statusAddr, linkedToken.Token)
                             ?? throw new InvalidDataException();
                }

                cancel.ThrowIfCancellationRequested();
            }
            catch (Exception e) when (e is JsonException or HttpRequestException or InvalidDataException)
            {
                data.Status = ServerStatusCode.Offline;
                return;
            }

            data.Status = ServerStatusCode.Online;
            data.Name = status.Name;
            data.GameName = status.GameName;
            data.PlayerCount = status.PlayerCount;
            data.SoftMaxPlayerCount = status.SoftMaxPlayerCount;
        }
        catch (OperationCanceledException)
        {
            data.Status = ServerStatusCode.Offline;
        }
    }

    public void Refresh()
    {
        // TODO: This refreshes everything.
        // Which means if you're hitting refresh on your home page, it'll refresh the servers list too.
        // This is wasteful.

        foreach (var datum in _cachedData.Values)
        {
            if (!datum.DidInitialStatusUpdate)
                continue;

            datum.Cancellation?.Cancel();

            UpdateStatusFor(datum);
        }
    }

    public void Clear()
    {
        foreach (var value in _cachedData.Values)
        {
            value.Cancellation?.Cancel();
        }

        _cachedData.Clear();
    }

    private sealed record ServerStatus(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("game_name")] string? GameName,
        [property: JsonPropertyName("players")]
        int PlayerCount,
        [property: JsonPropertyName("soft_max_players")]
        int SoftMaxPlayerCount);

    private sealed class CacheReg
    {
        public readonly ServerStatusData Data;
        public readonly SemaphoreSlim Semaphore = new(1);
        public CancellationTokenSource? Cancellation;
        public bool DidInitialStatusUpdate;

        public CacheReg(ServerStatusData data)
        {
            Data = data;
        }
    }
}
