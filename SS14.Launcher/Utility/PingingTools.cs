using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SS14.Launcher.Utility;

public static class PingingTools
{
    /// <summary>
    /// Gets the ping time for a host.
    /// </summary>
    public static async Task<long> GetPingTime(string? host)
    {
        if (host == null) return -1;
        host = NormalizeToHost(host);
        if (host == string.Empty) return -1;

        const int HowManyPings = 3;
        const int FailedPingPenalty = 3;
        long roundtripTime = 0;
        int failedPingCounter = 0;

        for (int i = 0; i < HowManyPings; i++)
        {
            try
            {
                using (var pingSender = new System.Net.NetworkInformation.Ping())
                {
                    var reply = await pingSender.SendPingAsync(host, 1000);
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        roundtripTime+=reply.RoundtripTime;
                    }
                    else if (reply.Status == System.Net.NetworkInformation.IPStatus.TimedOut)
                    {
                        failedPingCounter++;
                        roundtripTime += FailedPingPenalty;
                        if (failedPingCounter >= HowManyPings) return -2;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred during pinging server {ServerAddress}", host);
                failedPingCounter++;
                roundtripTime += FailedPingPenalty;
                if (failedPingCounter >= HowManyPings) return -2;
            }

        }
        return roundtripTime/ HowManyPings;
    }

    public static string NormalizeToHost(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri result))
        {
            return result.Host;
        }

        return string.Empty;
    }

}
