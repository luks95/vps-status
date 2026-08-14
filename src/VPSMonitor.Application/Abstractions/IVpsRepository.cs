using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Abstractions;

public interface IVpsRepository
{
    Task<IReadOnlyList<VpsServer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<VpsServer?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(VpsServer server, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
