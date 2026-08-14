using VPSMonitor.Application.Abstractions;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Servers;

public sealed class VpsManager(IVpsRepository repository, ICredentialVault credentialVault)
{
    public Task<IReadOnlyList<VpsServer>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    public async Task SaveAsync(VpsServer server, string? password, CancellationToken cancellationToken = default)
    {
        server.Validate();
        if (server.Id == Guid.Empty) throw new ArgumentException("La VPS debe tener un identificador.", nameof(server));

        var existing = await repository.GetAsync(server.Id, cancellationToken);
        string? previousPassword = null;

        if (password is not null)
        {
            if (existing is not null)
            {
                previousPassword = await credentialVault.GetPasswordAsync(server.Id, cancellationToken);
            }
            await credentialVault.StoreAsync(server.Id, server.Username, password, cancellationToken);
        }
        else if (existing is null)
        {
            throw new ArgumentException("La contraseña es obligatoria para una VPS nueva.", nameof(password));
        }

        try
        {
            await repository.SaveAsync(server, cancellationToken);
        }
        catch
        {
            if (password is not null)
            {
                if (previousPassword is null)
                {
                    await credentialVault.DeleteAsync(server.Id, CancellationToken.None);
                }
                else
                {
                    await credentialVault.StoreAsync(server.Id, existing!.Username, previousPassword, CancellationToken.None);
                }
            }

            throw;
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetAsync(id, cancellationToken);
        var previousPassword = await credentialVault.GetPasswordAsync(id, cancellationToken);
        await credentialVault.DeleteAsync(id, cancellationToken);
        try
        {
            await repository.DeleteAsync(id, cancellationToken);
        }
        catch
        {
            if (existing is not null && previousPassword is not null)
            {
                await credentialVault.StoreAsync(id, existing.Username, previousPassword, CancellationToken.None);
            }
            throw;
        }
    }
}
