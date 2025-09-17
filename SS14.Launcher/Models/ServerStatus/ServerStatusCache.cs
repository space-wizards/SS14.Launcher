using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Splat;
using SS14.Launcher.Api;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.ServerStatus;

/// <summary>
///     Caches information pulled from servers and updates it asynchronously.
/// </summary>
public sealed class ServerStatusCache : IServerSource
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

            ServerApi.ServerStatus status;
            try
            {
                // await Task.Delay(Random.Shared.Next(150, 5000), cancel);

                using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    linkedToken.CancelAfter(ConfigConstants.ServerStatusTimeout);

                    status = await http.GetFromJsonAsync<ServerApi.ServerStatus>(statusAddr, linkedToken.Token)
                             ?? throw new InvalidDataException();
                }

                cancel.ThrowIfCancellationRequested();
            }
            catch (Exception e) when (e is JsonException or HttpRequestException or InvalidDataException or IOException
                                          or SocketException)
            {
                data.Status = ServerStatusCode.Offline;
                return;
            }

            ApplyStatus(data, status);
        }
        catch (OperationCanceledException)
        {
            data.Status = ServerStatusCode.Offline;
        }
    }

    public static void ApplyStatus(ServerStatusData data, ServerApi.ServerStatus status)
    {
        data.Status = ServerStatusCode.Online;
        data.Name = status.Name;
        data.PlayerCount = Math.Max(0, status.PlayerCount);
        data.SoftMaxPlayerCount = Math.Max(0, status.SoftMaxPlayerCount);

        switch (status.RunLevel)
        {
            case ServerApi.GameRunLevel.InRound:
                data.RoundStatus = GameRoundStatus.InRound;
                break;
            case ServerApi.GameRunLevel.PostRound:
            case ServerApi.GameRunLevel.PreRoundLobby:
                data.RoundStatus = GameRoundStatus.InLobby;
                break;
            default:
                data.RoundStatus = GameRoundStatus.Unknown;
                break;
        }

        if (status.RoundStartTime != null)
        {
            data.RoundStartTime = DateTime.Parse(status.RoundStartTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        var baseTags = status.Tags ?? Array.Empty<string>();
        var inferredTags = ServerTagInfer.InferTags(status);

        data.Tags = baseTags.Concat(inferredTags).ToArray();
    }

    public static async void UpdateInfoForCore(ServerStatusData data, Func<CancellationToken, Task<ServerInfo?>> fetch)
    {
        if (data.StatusInfo == ServerStatusInfoCode.Fetching)
            return;

        if (data.Status != ServerStatusCode.Online)
        {
            Log.Error("Refusing to fetch info for server {Server} before we know it's online", data.Address);
            return;
        }

        data.InfoCancel?.Cancel();
        data.InfoCancel = new CancellationTokenSource();
        var cancel = data.InfoCancel.Token;

        data.StatusInfo = ServerStatusInfoCode.Fetching;

        ServerInfo info;
        try
        {
            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                linkedToken.CancelAfter(ConfigConstants.ServerStatusTimeout);

                info = await fetch(linkedToken.Token) ?? throw new InvalidDataException();
            }

            cancel.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            data.StatusInfo = ServerStatusInfoCode.NotFetched;
            return;
        }
        catch (Exception e) when (e is JsonException or HttpRequestException or InvalidDataException)
        {
            data.StatusInfo = ServerStatusInfoCode.Error;
            return;
        }

        data.StatusInfo = ServerStatusInfoCode.Fetched;
        data.Description = info.Desc;
        data.Links = info.Links;
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
            datum.Data.InfoCancel?.Cancel();

            datum.Data.StatusInfo = ServerStatusInfoCode.NotFetched;
            datum.Data.Links = null;
            datum.Data.Description = null;

            UpdateStatusFor(datum);
        }
    }

    public void Clear()
    {
        foreach (var value in _cachedData.Values)
        {
            value.Cancellation?.Cancel();
            value.Data.InfoCancel?.Cancel();
        }

        _cachedData.Clear();
    }

    void IServerSource.UpdateInfoFor(ServerStatusData statusData)
    {
        UpdateInfoForCore(statusData, async cancel =>
        {
            var uriBuilder = new UriBuilder(UriHelper.GetServerInfoAddress(statusData.Address));
            uriBuilder.Query = "?can_skip_build=1";
            return await _http.GetFromJsonAsync<ServerInfo>(uriBuilder.ToString(), cancel);
        });
    }

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
