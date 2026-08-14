using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Domain.Settings;

public sealed record AppSettings(
    AlertThresholds GlobalThresholds,
    bool HasShownTrayHint)
{
    public static AppSettings Default { get; } = new(new AlertThresholds(), false);
}
