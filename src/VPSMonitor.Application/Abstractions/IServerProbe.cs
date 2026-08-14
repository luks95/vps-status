using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Abstractions;

public interface IServerProbe
{
    Task<ProbeResult> ProbeAsync(
        VpsServer server,
        string password,
        CancellationToken cancellationToken = default);

    void Close(Guid serverId);
}

public sealed record ProbeResult(
    bool IsSuccess,
    RawSystemSnapshot? Snapshot,
    double? LatencyMilliseconds,
    LatencyMethod LatencyMethod,
    ConnectionStatus FailureStatus,
    string? Message,
    string? ObservedHostKeyFingerprint)
{
    public static ProbeResult Success(
        RawSystemSnapshot snapshot,
        double? latencyMilliseconds,
        LatencyMethod latencyMethod,
        string fingerprint) =>
        new(true, snapshot, latencyMilliseconds, latencyMethod, ConnectionStatus.Online, null, fingerprint);

    public static ProbeResult Failure(
        ConnectionStatus status,
        string message,
        string? observedFingerprint = null) =>
        new(false, null, null, LatencyMethod.Unavailable, status, message, observedFingerprint);
}
