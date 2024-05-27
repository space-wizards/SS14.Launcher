using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Utility;

public sealed class UrlFallbackSet(ImmutableArray<string> urls)
{
    public static readonly TimeSpan AttemptDelay = TimeSpan.FromSeconds(3);

    public readonly ImmutableArray<string> Urls = urls;

    public async Task<T?> GetFromJsonAsync<T>(HttpClient client, CancellationToken cancel = default) where T : notnull
    {
        var msg = await GetAsync(client, cancel).ConfigureAwait(false);
        msg.EnsureSuccessStatusCode();

        return await msg.Content.ReadFromJsonAsync<T>(cancel).ConfigureAwait(false);
    }

    public async Task<byte[]> GetByteArrayAsync(HttpClient client, CancellationToken cancel = default)
    {
        var msg = await GetAsync(client, cancel).ConfigureAwait(false);
        msg.EnsureSuccessStatusCode();

        return await msg.Content.ReadAsByteArrayAsync(cancel).ConfigureAwait(false);
    }

    public async Task<string> GetStringAsync(HttpClient client, CancellationToken cancel = default)
    {
        var msg = await GetAsync(client, cancel).ConfigureAwait(false);
        msg.EnsureSuccessStatusCode();

        return await msg.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> GetAsync(HttpClient httpClient, CancellationToken cancel = default)
    {
        return await SendAsync(httpClient, url => new HttpRequestMessage(HttpMethod.Get, url), cancel)
            .ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        Func<string, HttpRequestMessage> builder,
        CancellationToken cancel = default)
    {
        var (response, index) = await HappyEyeballsHttp.ParallelTask(
            Urls.Length,
            (i, token) => AttemptConnection(httpClient, builder(Urls[i]), token),
            AttemptDelay,
            cancel).ConfigureAwait(false);

        Log.Verbose("Successfully connected to {Url}", Urls[index]);

        return response;
    }

    private static async Task<HttpResponseMessage> AttemptConnection(
        HttpClient httpClient,
        HttpRequestMessage message,
        CancellationToken cancel)
    {
        // if (new Random().Next(2) == 0)
        // {
        //     Log.Error("Dropped the URL: {Message}", message);
        //     throw new InvalidOperationException("OOPS");
        // }

        var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancel
        ).ConfigureAwait(false);

        return response;
    }

    public static UrlFallbackSet operator +(UrlFallbackSet set, string s)
    {
        return new UrlFallbackSet([..set.Urls.Select(x => x + s)]);
    }
}
