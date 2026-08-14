using VPSMonitor.Domain.Settings;

namespace VPSMonitor.Application.Abstractions;

public interface IAppSettingsRepository
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
