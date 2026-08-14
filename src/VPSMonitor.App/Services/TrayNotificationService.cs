using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using VPSMonitor.Application.Abstractions;

namespace VPSMonitor.App.Services;

public sealed class TrayNotificationService : IUserNotificationService, IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayNotificationService()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir VPS Monitor", null, (_, _) => RestoreRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Shield,
            Text = "VPS Monitor",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreRequested?.Invoke();
    }

    public event Action? RestoreRequested;
    public event Action? ExitRequested;

    public void Show(string title, string message, NotificationSeverity severity)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = severity switch
        {
            NotificationSeverity.Critical => Forms.ToolTipIcon.Error,
            NotificationSeverity.Warning => Forms.ToolTipIcon.Warning,
            _ => Forms.ToolTipIcon.Info
        };
        _notifyIcon.ShowBalloonTip(5_000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
