using VPSMonitor.Application.Monitoring;

namespace VPSMonitor.App.ViewModels;

public sealed record AlertItemViewModel(
    string ServerName,
    string Title,
    string Message,
    string Time,
    bool IsRecovery)
{
    public static AlertItemViewModel From(AlertEvent alert, string serverName) => new(
        serverName,
        alert.IsRecovery ? "Recuperado" : KindName(alert.Kind),
        alert.Message,
        alert.OccurredAt.ToLocalTime().ToString("HH:mm:ss"),
        alert.IsRecovery);

    private static string KindName(AlertKind kind) => kind switch
    {
        AlertKind.Cpu => "CPU elevada",
        AlertKind.Memory => "Memoria elevada",
        AlertKind.Disk => "Uso de disco alto",
        AlertKind.Latency => "Latencia elevada",
        AlertKind.Unreachable => "VPS inaccesible",
        _ => "Alerta"
    };
}
