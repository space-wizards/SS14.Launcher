using Serilog;
using SS14.Launcher.Models.ServerStatus;
using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SS14.Launcher.Utility;

public static class PingingTools
{
    /// <summary>
    /// Gets the ping time for a host.
    /// </summary>
    public static async Task<PingStatus> GetPingTime(string? unparsedHost)
    {
        if (unparsedHost == null || !UriHelper.TryParseSs14Uri(unparsedHost, out var parssedHost))
        {
            Log.Warning("Server has invalid URI");
            return new PingError();
        }
        var host = parssedHost.Host;
        if (string.IsNullOrEmpty(host)) return new PingError();

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
                else if(reply.Status == IPStatus.TimedOut)
                {
                    failedPingCounter++;
                }
                else
                {
                    return new PingError();
                }
            }
            catch (PingException ex) when (ex.InnerException is SocketException)
            {
                Log.Warning("Failed to resolve host for ping: {ServerAddress}", host);
                return new PingError();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "An error occurred during pinging server: {ServerAddress}", host);
                return new PingError();
            }

            // Bail if it's failing too much
            if (failedPingCounter >= MaxFailedPings)
                return new PingTimedOut();

            if (successfulPings >= TargetSuccessfulPings)
                return new PingTime((int)(roundtripTime / TargetSuccessfulPings));

            await Task.Delay(DelayBetweenPingsMs);
        }
    }
}
