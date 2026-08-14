namespace VPSMonitor.Domain.Monitoring;

public static class MetricCalculator
{
    public static SystemMetrics Calculate(
        RawSystemSnapshot? previous,
        RawSystemSnapshot current,
        double? latencyMilliseconds,
        LatencyMethod latencyMethod)
    {
        var memoryUsed = SaturatingSubtract(current.MemoryTotalBytes, current.MemoryAvailableBytes);
        var swapUsed = SaturatingSubtract(current.SwapTotalBytes, current.SwapFreeBytes);
        var primaryDisk = SelectPrimaryFileSystem(current.FileSystems);
        var elapsedSeconds = previous is null
            ? 0
            : Math.Max(0, (current.CapturedAt - previous.CapturedAt).TotalSeconds);

        return new SystemMetrics(
            current.CapturedAt,
            CalculateCpu(previous?.Cpu, current.Cpu),
            memoryUsed,
            current.MemoryTotalBytes,
            Percent(memoryUsed, current.MemoryTotalBytes),
            swapUsed,
            current.SwapTotalBytes,
            primaryDisk?.UsedPercent ?? 0,
            primaryDisk?.UsedBytes ?? 0,
            primaryDisk?.TotalBytes ?? 0,
            Rate(previous?.NetworkReceivedBytes, current.NetworkReceivedBytes, elapsedSeconds),
            Rate(previous?.NetworkTransmittedBytes, current.NetworkTransmittedBytes, elapsedSeconds),
            Rate(previous?.DiskReadBytes, current.DiskReadBytes, elapsedSeconds),
            Rate(previous?.DiskWrittenBytes, current.DiskWrittenBytes, elapsedSeconds),
            current.Load1,
            current.Load5,
            current.Load15,
            current.ProcessCount,
            current.Uptime,
            current.TemperatureCelsius,
            latencyMilliseconds,
            latencyMethod,
            current.Distribution,
            current.KernelVersion);
    }

    public static double CalculateCpu(CpuCounters? previous, CpuCounters current)
    {
        if (previous is null || current.Total < previous.Total || current.IdleTotal < previous.IdleTotal)
        {
            return 0;
        }

        var totalDelta = current.Total - previous.Total;
        var idleDelta = current.IdleTotal - previous.IdleTotal;
        if (totalDelta == 0) return 0;

        return Math.Clamp((totalDelta - Math.Min(idleDelta, totalDelta)) * 100d / totalDelta, 0, 100);
    }

    private static FileSystemUsage? SelectPrimaryFileSystem(IReadOnlyList<FileSystemUsage> fileSystems) =>
        fileSystems.FirstOrDefault(fileSystem => fileSystem.MountPoint == "/")
        ?? fileSystems.OrderByDescending(fileSystem => fileSystem.TotalBytes).FirstOrDefault();

    private static ulong SaturatingSubtract(ulong left, ulong right) => left >= right ? left - right : 0;

    private static double Percent(ulong value, ulong total) => total == 0 ? 0 : value * 100d / total;

    private static double Rate(ulong? previous, ulong current, double elapsedSeconds)
    {
        if (previous is null || elapsedSeconds <= 0 || current < previous.Value) return 0;
        return (current - previous.Value) / elapsedSeconds;
    }
}
