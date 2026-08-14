namespace VPSMonitor.Domain.Monitoring;

public enum ConnectionStatus
{
    Disabled,
    Connecting,
    Online,
    Warning,
    Reconnecting,
    Offline,
    AuthenticationFailed,
    HostKeyUnknown,
    HostKeyChanged
}

public enum LatencyMethod
{
    Unavailable,
    Icmp,
    Tcp
}
