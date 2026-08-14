using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using VPSMonitor.Domain.Monitoring;

namespace VPSMonitor.Infrastructure.Network;

public sealed class LatencyProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(1_500);

    public async Task<(double? Milliseconds, LatencyMethod Method)> MeasureAsync(
        string host,
        int sshPort,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, (int)Timeout.TotalMilliseconds).WaitAsync(Timeout, cancellationToken);
            if (reply.Status == IPStatus.Success) return (reply.RoundtripTime, LatencyMethod.Icmp);
        }
        catch (Exception exception) when (exception is PingException or TimeoutException or OperationCanceledException)
        {
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
        }

        try
        {
            using var tcpClient = new TcpClient();
            var stopwatch = Stopwatch.StartNew();
            await tcpClient.ConnectAsync(host, sshPort, cancellationToken).AsTask().WaitAsync(Timeout, cancellationToken);
            stopwatch.Stop();
            return (stopwatch.Elapsed.TotalMilliseconds, LatencyMethod.Tcp);
        }
        catch (Exception exception) when (exception is SocketException or TimeoutException or OperationCanceledException)
        {
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
            return (null, LatencyMethod.Unavailable);
        }
    }
}
