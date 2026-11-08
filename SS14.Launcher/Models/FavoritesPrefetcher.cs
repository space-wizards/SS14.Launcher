using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models;

/// <summary>
/// Downloads updates for the user's favorite servers in the background, so they're ready by the time the user joins.
/// </summary>
/// <remarks>
/// Background downloads are cancelled when the user connects to a server, see <see cref="Updater.RunBackgroundUpdateAsync"/>.
/// </remarks>
public sealed class FavoritesPrefetcher
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly DataManager _cfg = Locator.Current.GetRequiredService<DataManager>();
    private readonly Updater _updater = Locator.Current.GetRequiredService<Updater>();
    private readonly HttpClient _http = Locator.Current.GetRequiredService<HttpClient>();

    public async void Start()
    {
        try
        {
            // Let the launcher finish starting up and settle before reaching out to servers
            await Task.Delay(StartDelay);

            while (true)
            {
                await PrefetchAllAsync();
                await Task.Delay(Interval);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Exception in favorites prefetcher, giving up");
        }
    }

    private async Task PrefetchAllAsync()
    {
        foreach (var favorite in _cfg.FavoriteServers.Items.ToArray())
        {
            if (!_cfg.GetCVar(CVars.PrefetchFavoriteServers))
                return;

            try
            {
                await PrefetchAsync(favorite.Address);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to prefetch favorite server {Address}", favorite.Address);
            }
        }
    }

    private async Task PrefetchAsync(string address)
    {
        if (!UriHelper.TryParseSs14Uri(address, out var parsedAddress))
            return;

        using var cancel = new CancellationTokenSource(ConfigConstants.ServerStatusTimeout);
        var info = await _http.GetFromJsonAsync<ServerInfo>(UriHelper.GetServerInfoAddress(parsedAddress), cancel.Token);

        // Skill issue
        if (info?.BuildInformation is not { } buildInfo)
            return;

        // Don't pull if the privacy policy hasn't been accepted. They already have our ip from poking status but ehh
        if (info.PrivacyPolicy is { } policy
            && !(_cfg.HasAcceptedPrivacyPolicy(policy.Identifier, out var version) && version == policy.Version))
        {
            return;
        }

        Connector.InferBuildDownloadUrls(info, parsedAddress);

        Log.Debug("Prefetching favorite server {Address}", address);
        await _updater.RunBackgroundUpdateAsync(buildInfo);
    }
}
