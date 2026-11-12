using VPSMonitor.Application.Abstractions;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Servers;

public sealed class VpsManager(IVpsRepository repository, ICredentialVault credentialVault)
{
    public Task<IReadOnlyList<VpsServer>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    /// <summary>
    /// Guarda la VPS y su secreto (contraseña, o passphrase de la clave privada).
    /// <paramref name="secret"/> null conserva el secreto guardado; una cadena vacía con clave privada
    /// elimina la passphrase guardada.
    /// </summary>
    public async Task SaveAsync(VpsServer server, string? secret, CancellationToken cancellationToken = default)
    {
        server.Validate();
        if (server.Id == Guid.Empty) throw new ArgumentException("La VPS debe tener un identificador.", nameof(server));

        var existing = await repository.GetAsync(server.Id, cancellationToken);
        var isKeyAuth = server.AuthMethod == SshAuthMethod.PrivateKey;

        if (isKeyAuth)
        {
            // Un secreto guardado de otro método o de otra clave no sirve como passphrase.
            var keyChanged = existing is null ||
                existing.AuthMethod != SshAuthMethod.PrivateKey ||
                !string.Equals(existing.PrivateKeyPath, server.PrivateKeyPath, StringComparison.OrdinalIgnoreCase);
            if (secret is null && keyChanged) secret = string.Empty;
        }
        else
        {
            if (string.IsNullOrEmpty(secret)) secret = null;
            if (secret is null && (existing is null || existing.AuthMethod != SshAuthMethod.Password))
            {
                throw new ArgumentException("La contraseña es obligatoria.", nameof(secret));
            }
        }

        string? previousSecret = null;
        if (secret is not null)
        {
            if (existing is not null)
            {
                previousSecret = await credentialVault.GetPasswordAsync(server.Id, cancellationToken);
            }

            if (secret.Length == 0)
            {
                await credentialVault.DeleteAsync(server.Id, cancellationToken);
            }
            else
            {
                await credentialVault.StoreAsync(server.Id, server.Username, secret, cancellationToken);
            }
        }

        try
        {
            await repository.SaveAsync(server, cancellationToken);
        }
        catch
        {
            if (secret is not null)
            {
                if (previousSecret is null)
                {
                    await credentialVault.DeleteAsync(server.Id, CancellationToken.None);
                }
                else
                {
                    await credentialVault.StoreAsync(server.Id, existing!.Username, previousSecret, CancellationToken.None);
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
