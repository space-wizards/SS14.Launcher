using Serilog;
using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace SS14.Launcher.Utility;

public static class PingingTools
{
    /// <summary>
    /// Gets the ping time for a host.
    /// </summary>
    public static async Task<int> GetPingTime(string? unparsedHost)
    {
        if (unparsedHost == null || !UriHelper.TryParseSs14Uri(unparsedHost, out var parssedHost))
        {
            Log.Warning("Server has invalid URI");
            return -1;
        }
        var host = parssedHost.Host;
        if (string.IsNullOrEmpty(host)) return -1;

        const int TargetSuccessfulPings = 3;
        const int MaxFailedPings = 2;

        long roundtripTime = 0;
        int failedPingCounter = 0;
        int successfulPings = 0;
        const int DelayBetweenPingsMs = 250;

        using var pingSender = new Ping();

        while (true)
        {
            try
            {
                var reply = await pingSender.SendPingAsync(host, 3000);

                if (reply.Status == IPStatus.Success)
                {
                    roundtripTime += reply.RoundtripTime;
                    successfulPings++;
                }
                else
                {
                    failedPingCounter++;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred during pinging server {ServerAddress}", host);
                failedPingCounter++;
            }

            // Bail if it's failing too much
            if (failedPingCounter >= MaxFailedPings)
                return -2;

            if (successfulPings >= TargetSuccessfulPings)
                return (int)(roundtripTime / TargetSuccessfulPings);

            await Task.Delay(DelayBetweenPingsMs);
        }
    }
}
