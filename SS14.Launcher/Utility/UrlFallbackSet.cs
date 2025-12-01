using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Utility;

public sealed class UrlFallbackSet
{
    public static readonly TimeSpan AttemptDelay = TimeSpan.FromSeconds(3);

    public readonly ImmutableArray<string> Urls;
    public readonly UrlFallbackSetStats Stats;

    public UrlFallbackSet(ImmutableArray<string> urls, UrlFallbackSetStats? stats = null)
    {
        if (urls.Length == 0)
            throw new ArgumentException("Urls must not be empty.", nameof(urls));

        Urls = urls;

        if (stats != null)
        {
            if (stats.RequestCount.Length != urls.Length)
                throw new ArgumentException("Stats has wrong length!");
            Stats = stats;
        }
        else
        {
            Stats = new UrlFallbackSetStats(urls.Length);
        }
    }

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

    public async Task<HttpResponseMessage> PostAsync(HttpClient httpClient, HttpContent content,
        CancellationToken cancel = default)
    {
        return await SendAsync(httpClient, url => new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        }, cancel);
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

        Stats.AddSuccessfulRequest(index);

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

        response.EnsureSuccessStatusCode();

        return response;
    }

    public string GetMostSuccessfulUrl()
    {
        var maxUrl = Enumerable.Range(0, Urls.Length).MaxBy(i => Stats.RequestCount[i]);
        return Urls[maxUrl];
    }

    public static UrlFallbackSet operator +(UrlFallbackSet set, string s)
    {
        return new UrlFallbackSet([..set.Urls.Select(x => x + s)], set.Stats);
    }

    public static UrlFallbackSet FromSingle(Uri url)
    {
        return FromSingle(url.ToString());
    }

    public static UrlFallbackSet FromSingle(string url)
    {
        return new UrlFallbackSet([url]);
    }
}

public sealed class UrlFallbackSetStats(int countUrls)
{
    // I don't actually think we're gonna have more than 2 billion requests in the app's lifetime,
    // but I definitely know we're never gonna have 2^63.
    public readonly long[] RequestCount = new long[countUrls];

    public void AddSuccessfulRequest(int idx)
    {
        Interlocked.Increment(ref RequestCount[idx]);
    }
}

public static class UrlFallbackSetHttpClientExtensions
{
    public static async Task<HttpResponseMessage> PostAsJsonAsync<TValue>(
        this HttpClient httpClient,
        UrlFallbackSet fallbackSet,
        TValue value,
        CancellationToken cancel = default)
    {
        var content = JsonContent.Create(value);
        return await fallbackSet.PostAsync(httpClient, content, cancel).ConfigureAwait(false);
    }
}
