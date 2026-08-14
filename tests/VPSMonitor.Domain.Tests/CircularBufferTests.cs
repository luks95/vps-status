using VPSMonitor.Domain.Monitoring;

namespace VPSMonitor.Domain.Tests;

public sealed class CircularBufferTests
{
    [Fact]
    public void Add_KeepsOnlyNewestItemsInChronologicalOrder()
    {
        var buffer = new CircularBuffer<int>(3);

        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        Assert.Equal([2, 3, 4], buffer.Snapshot());
    }

    [Fact]
    public void Clear_RemovesAllItemsAndAllowsReuse()
    {
        var buffer = new CircularBuffer<string>(2);
        buffer.Add("a");
        buffer.Clear();
        buffer.Add("b");

        Assert.Equal(["b"], buffer.Snapshot());
    }
}
