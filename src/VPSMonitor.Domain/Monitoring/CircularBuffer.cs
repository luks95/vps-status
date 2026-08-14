using System.Collections;

namespace VPSMonitor.Domain.Monitoring;

public sealed class CircularBuffer<T> : IReadOnlyCollection<T>
{
    private readonly T[] _items;
    private int _next;

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new T[capacity];
    }

    public int Capacity => _items.Length;
    public int Count { get; private set; }

    public void Add(T item)
    {
        _items[_next] = item;
        _next = (_next + 1) % Capacity;
        if (Count < Capacity) Count++;
    }

    public void Clear()
    {
        Array.Clear(_items);
        _next = 0;
        Count = 0;
    }

    public IReadOnlyList<T> Snapshot()
    {
        if (Count == 0) return [];

        var result = new T[Count];
        var start = Count == Capacity ? _next : 0;
        for (var i = 0; i < Count; i++)
        {
            result[i] = _items[(start + i) % Capacity];
        }

        return result;
    }

    public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
