using System.Collections.ObjectModel;
using System.Globalization;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.App.ViewModels;

public sealed class ServerItemViewModel : ObservableObject
{
    private const int MaxPoints = 30;
    private VpsServer _server;
    private ConnectionStatus _status = ConnectionStatus.Connecting;
    private SystemMetrics? _metrics;
    private string? _statusMessage;

    public ServerItemViewModel(VpsServer server)
    {
        _server = server;
        CpuSeries = [CreateSeries(CpuValues, new SKColor(39, 194, 193), true)];
        MemorySeries = [CreateSeries(MemoryValues, new SKColor(59, 156, 255), true)];
        NetworkSeries =
        [
            CreateSeries(NetworkReceiveValues, new SKColor(59, 156, 255), false, "Descarga"),
            CreateSeries(NetworkTransmitValues, new SKColor(39, 194, 193), false, "Subida")
        ];
        LatencySeries = [CreateSeries(LatencyValues, new SKColor(39, 194, 193), false)];
    }

    public VpsServer Server => _server;
    public Guid Id => _server.Id;
    public string Name => _server.Name;
    public string Endpoint => $"{_server.Host}:{_server.Port}";
    public string Username => _server.Username;
    public ConnectionStatus Status => _status;
    public SystemMetrics? Metrics => _metrics;
    public string? StatusMessage => _statusMessage;

    public string StatusLabel => _status switch
    {
        ConnectionStatus.Online => "En línea",
        ConnectionStatus.Warning => "Advertencia",
        ConnectionStatus.Connecting => "Conectando",
        ConnectionStatus.Reconnecting => "Reconectando",
        ConnectionStatus.AuthenticationFailed => "Credenciales incorrectas",
        ConnectionStatus.HostKeyUnknown => "Huella por aprobar",
        ConnectionStatus.HostKeyChanged => "Huella modificada",
        ConnectionStatus.Disabled => "Deshabilitada",
        _ => "Sin conexión"
    };

    public string StatusColor => _status switch
    {
        ConnectionStatus.Online => "#42D17E",
        ConnectionStatus.Warning or ConnectionStatus.Reconnecting or ConnectionStatus.HostKeyUnknown => "#FFB020",
        ConnectionStatus.Connecting => "#3B9CFF",
        ConnectionStatus.Disabled => "#7E8B98",
        _ => "#FF5C6C"
    };

    public string CpuText => _metrics is null ? "—" : $"{_metrics.CpuPercent:F0}%";
    public string MemoryText => _metrics is null ? "—" : $"{FormatBytes(_metrics.MemoryUsedBytes)} / {FormatBytes(_metrics.MemoryTotalBytes)}";
    public string MemoryPercentText => _metrics is null ? "—" : $"{_metrics.MemoryPercent:F0}%";
    public string DiskText => _metrics is null ? "—" : $"{FormatBytes(_metrics.DiskUsedBytes)} / {FormatBytes(_metrics.DiskTotalBytes)}";
    public string DiskPercentText => _metrics is null ? "—" : $"{_metrics.DiskUsedPercent:F0}%";
    public double DiskPercent => _metrics?.DiskUsedPercent ?? 0;
    public string LatencyText => _metrics?.LatencyMilliseconds is { } value ? $"{value:F0} ms" : "No disponible";
    public string LatencyMethodText => _metrics?.LatencyMethod switch
    {
        LatencyMethod.Icmp => "ICMP",
        LatencyMethod.Tcp => "TCP · puerto SSH",
        _ => "Sin medición"
    };
    public string UptimeText => _metrics is null ? "—" : FormatUptime(_metrics.Uptime);
    public string LoadText => _metrics is null ? "—" : _metrics.Load1.ToString("F2", CultureInfo.InvariantCulture);
    public string LoadDetails => _metrics is null ? "1m —   5m —   15m —" : $"1m {_metrics.Load1:F2}   5m {_metrics.Load5:F2}   15m {_metrics.Load15:F2}";
    public string ProcessesText => _metrics?.ProcessCount.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string TemperatureText => _metrics?.TemperatureCelsius is { } value ? $"{value:F0}°C" : "No disponible";
    public string DistributionText => _metrics?.Distribution ?? "Linux";
    public string KernelText => _metrics is null ? string.Empty : $"Kernel {_metrics.KernelVersion}";
    public string NetworkReceiveText => _metrics is null ? "↓ —" : $"↓ {FormatRate(_metrics.NetworkReceiveBytesPerSecond)}";
    public string NetworkTransmitText => _metrics is null ? "↑ —" : $"↑ {FormatRate(_metrics.NetworkTransmitBytesPerSecond)}";
    public string LastUpdatedText => _metrics is null ? "Sin datos" : $"Última actualización: {_metrics.CapturedAt.ToLocalTime():HH:mm:ss}";
    public string ChartSummary => _metrics is null
        ? "Todavía no hay métricas disponibles."
        : $"CPU {_metrics.CpuPercent:F0}%, memoria {_metrics.MemoryPercent:F0}%, disco {_metrics.DiskUsedPercent:F0}% y latencia {LatencyText}.";

    public ObservableCollection<double> CpuValues { get; } = [];
    public ObservableCollection<double> MemoryValues { get; } = [];
    public ObservableCollection<double> NetworkReceiveValues { get; } = [];
    public ObservableCollection<double> NetworkTransmitValues { get; } = [];
    public ObservableCollection<double> LatencyValues { get; } = [];
    public ISeries[] CpuSeries { get; }
    public ISeries[] MemorySeries { get; }
    public ISeries[] NetworkSeries { get; }
    public ISeries[] LatencySeries { get; }

    public Axis[] PercentageYAxes { get; } =
    [
        new Axis
        {
            MinLimit = 0,
            MaxLimit = 100,
            Labeler = value => $"{value:F0}%",
            LabelsPaint = new SolidColorPaint(new SKColor(168, 180, 192)),
            SeparatorsPaint = new SolidColorPaint(new SKColor(42, 53, 65), 1)
        }
    ];

    public Axis[] LatencyYAxes { get; } =
    [
        new Axis
        {
            MinLimit = 0,
            Labeler = value => $"{value:F0} ms",
            LabelsPaint = new SolidColorPaint(new SKColor(168, 180, 192)),
            SeparatorsPaint = new SolidColorPaint(new SKColor(42, 53, 65), 1)
        }
    ];

    public Axis[] RateYAxes { get; } =
    [
        new Axis
        {
            MinLimit = 0,
            Labeler = FormatRate,
            LabelsPaint = new SolidColorPaint(new SKColor(168, 180, 192)),
            SeparatorsPaint = new SolidColorPaint(new SKColor(42, 53, 65), 1)
        }
    ];

    public Axis[] TimeXAxes { get; } =
    [
        new Axis
        {
            MinLimit = 0,
            MaxLimit = MaxPoints - 1,
            Labeler = value => value switch
            {
                < 1 => "-60s",
                > 27 => "Ahora",
                _ => string.Empty
            },
            LabelsPaint = new SolidColorPaint(new SKColor(126, 139, 152)),
            SeparatorsPaint = new SolidColorPaint(new SKColor(42, 53, 65), 1)
        }
    ];

    public void ReplaceServer(VpsServer server)
    {
        _server = server;
        OnPropertyChanged(nameof(Server));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Endpoint));
        OnPropertyChanged(nameof(Username));
    }

    public void Apply(ServerMonitorState state)
    {
        _status = state.Status;
        _statusMessage = state.Message;
        if (state.Metrics is { } metrics)
        {
            _metrics = metrics;
            AddPoint(CpuValues, metrics.CpuPercent);
            AddPoint(MemoryValues, metrics.MemoryPercent);
            AddPoint(NetworkReceiveValues, metrics.NetworkReceiveBytesPerSecond);
            AddPoint(NetworkTransmitValues, metrics.NetworkTransmitBytesPerSecond);
            AddPoint(LatencyValues, metrics.LatencyMilliseconds ?? 0);
        }

        foreach (var property in MetricProperties) OnPropertyChanged(property);
    }

    private static readonly string[] MetricProperties =
    [
        nameof(Status), nameof(StatusLabel), nameof(StatusColor), nameof(StatusMessage), nameof(Metrics),
        nameof(CpuText), nameof(MemoryText), nameof(MemoryPercentText), nameof(DiskText), nameof(DiskPercentText),
        nameof(DiskPercent), nameof(LatencyText), nameof(LatencyMethodText), nameof(UptimeText), nameof(LoadText),
        nameof(LoadDetails), nameof(ProcessesText), nameof(TemperatureText), nameof(DistributionText),
        nameof(KernelText), nameof(NetworkReceiveText), nameof(NetworkTransmitText), nameof(LastUpdatedText),
        nameof(ChartSummary)
    ];

    private static LineSeries<double> CreateSeries(
        ObservableCollection<double> values,
        SKColor color,
        bool fill,
        string? name = null) => new()
        {
            Name = name,
            Values = values,
            GeometrySize = 0,
            LineSmoothness = 0.25,
            Stroke = new SolidColorPaint(color, 2),
            Fill = fill ? new SolidColorPaint(color.WithAlpha(28)) : null,
            AnimationsSpeed = TimeSpan.FromMilliseconds(150)
        };

    private static void AddPoint(ObservableCollection<double> values, double value)
    {
        values.Add(value);
        while (values.Count > MaxPoints) values.RemoveAt(0);
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }
        return $"{bytes:F1} {units[unit]}";
    }

    private static string FormatRate(double bytesPerSecond) => $"{FormatBytes(bytesPerSecond)}/s";

    private static string FormatUptime(TimeSpan uptime) => uptime.TotalDays >= 1
        ? $"{(int)uptime.TotalDays}d {uptime.Hours}h"
        : $"{uptime.Hours}h {uptime.Minutes}m";
}
