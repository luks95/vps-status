namespace VPSMonitor.Application.Abstractions;

public interface ICredentialVault
{
    Task StoreAsync(Guid serverId, string username, string password, CancellationToken cancellationToken = default);
    Task<string?> GetPasswordAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid serverId, CancellationToken cancellationToken = default);
}
