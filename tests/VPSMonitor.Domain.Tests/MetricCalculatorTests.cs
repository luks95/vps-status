using VPSMonitor.Domain.Monitoring;

namespace VPSMonitor.Domain.Tests;

public sealed class MetricCalculatorTests
{
    [Fact]
    public void Calculate_UsesDeltasAndRootFileSystem()
    {
        var start = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
        var previous = Snapshot(start, new CpuCounters(100, 0, 100, 700, 0, 0, 0, 0), 1_000, 2_000, 10_000, 20_000);
        var current = Snapshot(start.AddSeconds(2), new CpuCounters(150, 0, 150, 800, 0, 0, 0, 0), 3_000, 6_000, 14_000, 28_000);

        var result = MetricCalculator.Calculate(previous, current, 42, LatencyMethod.Icmp);

        Assert.Equal(50, result.CpuPercent, 3);
        Assert.Equal(1_000, result.NetworkReceiveBytesPerSecond);
        Assert.Equal(2_000, result.NetworkTransmitBytesPerSecond);
        Assert.Equal(2_000, result.DiskReadBytesPerSecond);
        Assert.Equal(4_000, result.DiskWriteBytesPerSecond);
        Assert.Equal(50, result.MemoryPercent, 3);
        Assert.Equal(60, result.DiskUsedPercent, 3);
        Assert.Equal(42, result.LatencyMilliseconds);
    }

    [Fact]
    public void Calculate_WhenCountersReset_ReturnsZeroRates()
    {
        var start = DateTimeOffset.Parse("2026-08-13T12:00:00Z");
        var previous = Snapshot(start, new CpuCounters(100, 0, 100, 700, 0, 0, 0, 0), 5_000, 5_000, 5_000, 5_000);
        var current = Snapshot(start.AddSeconds(2), new CpuCounters(1, 0, 1, 7, 0, 0, 0, 0), 100, 100, 100, 100);

        var result = MetricCalculator.Calculate(previous, current, null, LatencyMethod.Unavailable);

        Assert.Equal(0, result.CpuPercent);
        Assert.Equal(0, result.NetworkReceiveBytesPerSecond);
        Assert.Equal(0, result.DiskWriteBytesPerSecond);
    }

    private static RawSystemSnapshot Snapshot(
        DateTimeOffset capturedAt,
        CpuCounters cpu,
        ulong rx,
        ulong tx,
        ulong diskRead,
        ulong diskWrite) =>
        new(
            capturedAt,
            cpu,
            8_000,
            4_000,
            2_000,
            1_000,
            rx,
            tx,
            diskRead,
            diskWrite,
            [new FileSystemUsage("/dev/vda1", "/", 10_000, 6_000, 4_000)],
            0.64,
            0.52,
            0.45,
            146,
            TimeSpan.FromDays(18),
            54,
            "Ubuntu 24.04 LTS",
            "6.8.0");
}
