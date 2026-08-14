using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Monitoring;

public enum AlertKind
{
    Cpu,
    Memory,
    Disk,
    Latency,
    Unreachable
}

public sealed record AlertEvent(
    Guid ServerId,
    AlertKind Kind,
    bool IsRecovery,
    string Message,
    DateTimeOffset OccurredAt);

public sealed class AlertEvaluator(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<(Guid, AlertKind), AlertState> _states = [];

    public IReadOnlyList<AlertEvent> Evaluate(
        Guid serverId,
        SystemMetrics? metrics,
        bool isReachable,
        AlertThresholds thresholds)
    {
        var now = _timeProvider.GetUtcNow();
        var events = new List<AlertEvent>();

        EvaluateOne(serverId, AlertKind.Unreachable, !isReachable, isReachable, "La VPS no responde.", now, events);
        if (metrics is null) return events;

        EvaluateOne(serverId, AlertKind.Cpu,
            metrics.CpuPercent > thresholds.CpuPercent,
            metrics.CpuPercent <= thresholds.CpuPercent - 5,
            $"CPU en {metrics.CpuPercent:F0}%.", now, events);
        EvaluateOne(serverId, AlertKind.Memory,
            metrics.MemoryPercent > thresholds.MemoryPercent,
            metrics.MemoryPercent <= thresholds.MemoryPercent - 5,
            $"Memoria en {metrics.MemoryPercent:F0}%.", now, events);
        EvaluateOne(serverId, AlertKind.Disk,
            metrics.DiskUsedPercent > thresholds.DiskPercent,
            metrics.DiskUsedPercent <= thresholds.DiskPercent - 5,
            $"Disco en {metrics.DiskUsedPercent:F0}%.", now, events);

        if (metrics.LatencyMilliseconds is { } latency)
        {
            EvaluateOne(serverId, AlertKind.Latency,
                latency > thresholds.LatencyMilliseconds,
                latency <= thresholds.LatencyMilliseconds - 20,
                $"Latencia en {latency:F0} ms.", now, events);
        }

        return events;
    }

    private void EvaluateOne(
        Guid serverId,
        AlertKind kind,
        bool isOutside,
        bool isRecovered,
        string message,
        DateTimeOffset now,
        ICollection<AlertEvent> events)
    {
        var key = (serverId, kind);
        if (!_states.TryGetValue(key, out var state))
        {
            state = new AlertState();
            _states[key] = state;
        }

        if (!state.IsActive)
        {
            state.OutsideCount = isOutside ? state.OutsideCount + 1 : 0;
            state.RecoveryCount = 0;
            if (state.OutsideCount >= 3)
            {
                state.IsActive = true;
                state.LastNotification = now;
                events.Add(new AlertEvent(serverId, kind, false, message, now));
            }
            return;
        }

        state.RecoveryCount = isRecovered ? state.RecoveryCount + 1 : 0;
        if (state.RecoveryCount >= 2)
        {
            state.IsActive = false;
            state.OutsideCount = 0;
            events.Add(new AlertEvent(serverId, kind, true, "El valor volvió al rango normal.", now));
            return;
        }

        if (isOutside && now - state.LastNotification >= TimeSpan.FromMinutes(5))
        {
            state.LastNotification = now;
            events.Add(new AlertEvent(serverId, kind, false, message, now));
        }
    }

    private sealed class AlertState
    {
        public int OutsideCount { get; set; }
        public int RecoveryCount { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset LastNotification { get; set; }
    }
}
