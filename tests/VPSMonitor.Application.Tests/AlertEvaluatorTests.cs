using VPSMonitor.Application.Monitoring;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Tests;

public sealed class AlertEvaluatorTests
{
    [Fact]
    public void Evaluate_ActivatesAfterThreeSamplesAndRecoversAfterTwo()
    {
        var evaluator = new AlertEvaluator();
        var serverId = Guid.NewGuid();
        var thresholds = new AlertThresholds();

        Assert.Empty(evaluator.Evaluate(serverId, Metrics(cpu: 95), true, thresholds));
        Assert.Empty(evaluator.Evaluate(serverId, Metrics(cpu: 96), true, thresholds));
        var activated = evaluator.Evaluate(serverId, Metrics(cpu: 97), true, thresholds);

        Assert.Contains(activated, item => item.Kind == AlertKind.Cpu && !item.IsRecovery);
        Assert.Empty(evaluator.Evaluate(serverId, Metrics(cpu: 84), true, thresholds));
        var recovered = evaluator.Evaluate(serverId, Metrics(cpu: 84), true, thresholds);
        Assert.Contains(recovered, item => item.Kind == AlertKind.Cpu && item.IsRecovery);
    }

    [Fact]
    public void Evaluate_TracksEachServerIndependently()
    {
        var evaluator = new AlertEvaluator();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var thresholds = new AlertThresholds();

        evaluator.Evaluate(first, Metrics(cpu: 99), true, thresholds);
        evaluator.Evaluate(first, Metrics(cpu: 99), true, thresholds);
        var secondEvents = evaluator.Evaluate(second, Metrics(cpu: 99), true, thresholds);

        Assert.Empty(secondEvents);
    }

    private static SystemMetrics Metrics(double cpu) => new(
        DateTimeOffset.UtcNow,
        cpu,
        4_000,
        8_000,
        50,
        0,
        0,
        40,
        4_000,
        10_000,
        0,
        0,
        0,
        0,
        0.5,
        0.4,
        0.3,
        100,
        TimeSpan.FromDays(1),
        null,
        40,
        LatencyMethod.Icmp,
        "Ubuntu",
        "6.8");
}
