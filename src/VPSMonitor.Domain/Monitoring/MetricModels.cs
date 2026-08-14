namespace VPSMonitor.Domain.Monitoring;

public sealed record CpuCounters(
    ulong User,
    ulong Nice,
    ulong System,
    ulong Idle,
    ulong IoWait,
    ulong Irq,
    ulong SoftIrq,
    ulong Steal)
{
    public ulong IdleTotal => Idle + IoWait;
    public ulong Total => User + Nice + System + Idle + IoWait + Irq + SoftIrq + Steal;
}

public sealed record FileSystemUsage(
    string Device,
    string MountPoint,
    ulong TotalBytes,
    ulong UsedBytes,
    ulong AvailableBytes)
{
    public double UsedPercent => TotalBytes == 0 ? 0 : UsedBytes * 100d / TotalBytes;
}

public sealed record RawSystemSnapshot(
    DateTimeOffset CapturedAt,
    CpuCounters Cpu,
    ulong MemoryTotalBytes,
    ulong MemoryAvailableBytes,
    ulong SwapTotalBytes,
    ulong SwapFreeBytes,
    ulong NetworkReceivedBytes,
    ulong NetworkTransmittedBytes,
    ulong DiskReadBytes,
    ulong DiskWrittenBytes,
    IReadOnlyList<FileSystemUsage> FileSystems,
    double Load1,
    double Load5,
    double Load15,
    int ProcessCount,
    TimeSpan Uptime,
    double? TemperatureCelsius,
    string Distribution,
    string KernelVersion);

public sealed record SystemMetrics(
    DateTimeOffset CapturedAt,
    double CpuPercent,
    ulong MemoryUsedBytes,
    ulong MemoryTotalBytes,
    double MemoryPercent,
    ulong SwapUsedBytes,
    ulong SwapTotalBytes,
    double DiskUsedPercent,
    ulong DiskUsedBytes,
    ulong DiskTotalBytes,
    double NetworkReceiveBytesPerSecond,
    double NetworkTransmitBytesPerSecond,
    double DiskReadBytesPerSecond,
    double DiskWriteBytesPerSecond,
    double Load1,
    double Load5,
    double Load15,
    int ProcessCount,
    TimeSpan Uptime,
    double? TemperatureCelsius,
    double? LatencyMilliseconds,
    LatencyMethod LatencyMethod,
    string Distribution,
    string KernelVersion)
{
    public static SystemMetrics FromFirstSnapshot(
        RawSystemSnapshot snapshot,
        double? latencyMilliseconds,
        LatencyMethod latencyMethod) =>
        MetricCalculator.Calculate(null, snapshot, latencyMilliseconds, latencyMethod);
}

public sealed record ServerMonitorState(
    Guid ServerId,
    ConnectionStatus Status,
    SystemMetrics? Metrics,
    string? Message,
    DateTimeOffset UpdatedAt);
