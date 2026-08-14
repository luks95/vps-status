using VPSMonitor.Infrastructure.Linux;

namespace VPSMonitor.Infrastructure.Tests;

public sealed class LinuxSnapshotParserTests
{
    private const string Sample = """
        __CPU__
        cpu  100 2 50 800 10 3 5 1 0 0
        __MEM__
        MemTotal:        8000000 kB
        MemFree:          500000 kB
        MemAvailable:    3000000 kB
        SwapTotal:       2000000 kB
        SwapFree:        1500000 kB
        __NET__
        Inter-|   Receive                                                |  Transmit
         face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
            lo: 100 1 0 0 0 0 0 0 100 1 0 0 0 0 0 0
          eth0: 1000 10 0 0 0 0 0 0 2000 20 0 0 0 0 0 0
        __BLOCK__
        vda 10 0 200 0 20 0 400 0 0 0 0 0 0 0 0 0 0
        loop0 1 0 999 0 1 0 999 0 0 0 0
        __DF__
        Filesystem 1B-blocks Used Available Capacity Mounted on
        /dev/vda1 100000 60000 40000 60% /
        __LOAD__
        0.64 0.52 0.45 1/146 1000
        __UPTIME__
        1587600.00 1000.00
        __TEMP__
        54000
        __OS__
        Ubuntu 24.04 LTS
        6.8.0-40-generic
        __END__
        """;

    [Fact]
    public void Parse_ReadsAllCoreMetrics()
    {
        var capturedAt = DateTimeOffset.Parse("2026-08-13T12:00:00Z");

        var result = new LinuxSnapshotParser().Parse(Sample, capturedAt);

        Assert.Equal((ulong)8_192_000_000, result.MemoryTotalBytes);
        Assert.Equal((ulong)3_072_000_000, result.MemoryAvailableBytes);
        Assert.Equal((ulong)1_000, result.NetworkReceivedBytes);
        Assert.Equal((ulong)2_000, result.NetworkTransmittedBytes);
        Assert.Equal((ulong)102_400, result.DiskReadBytes);
        Assert.Equal((ulong)204_800, result.DiskWrittenBytes);
        Assert.Equal(146, result.ProcessCount);
        Assert.Equal(54, result.TemperatureCelsius);
        Assert.Equal("Ubuntu 24.04 LTS", result.Distribution);
        Assert.Equal("/", Assert.Single(result.FileSystems).MountPoint);
    }

    [Fact]
    public void Parse_WhenRequiredSectionIsMissing_ThrowsUsefulError()
    {
        var error = Assert.Throws<FormatException>(() =>
            new LinuxSnapshotParser().Parse("__CPU__\ncpu 1 2 3 4 5 6 7 8\n", DateTimeOffset.UtcNow));

        Assert.Contains("MEM", error.Message);
    }
}
