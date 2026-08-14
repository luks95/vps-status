namespace VPSMonitor.Application.Abstractions;

public interface IUserNotificationService
{
    void Show(string title, string message, NotificationSeverity severity);
}

public enum NotificationSeverity
{
    Information,
    Success,
    Warning,
    Critical
}
