using VPSMonitor.Application.Abstractions;
using VPSMonitor.Application.Servers;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Tests;

public sealed class VpsManagerTests
{
    [Fact]
    public async Task Save_WhenRepositoryFails_RestoresPreviousCredential()
    {
        var server = VpsServer.Create("Producción", "10.0.0.1", 22, "root");
        var repository = new FailingRepository(server);
        var vault = new MemoryVault("anterior");
        var manager = new VpsManager(repository, vault);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.SaveAsync(server with { Name = "Nuevo" }, "nueva"));

        Assert.Equal("anterior", await vault.GetPasswordAsync(server.Id));
    }

    [Fact]
    public async Task Delete_WhenRepositoryFails_RestoresCredential()
    {
        var server = VpsServer.Create("Producción", "10.0.0.1", 22, "root");
        var repository = new FailingRepository(server);
        var vault = new MemoryVault("anterior");
        var manager = new VpsManager(repository, vault);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DeleteAsync(server.Id));

        Assert.Equal("anterior", await vault.GetPasswordAsync(server.Id));
    }

    [Fact]
    public async Task Save_NewKeyServerWithoutPassphrase_DoesNotRequireSecretAndStoresNothing()
    {
        var repository = new InMemoryRepository();
        var vault = new MemoryVault(null);
        var manager = new VpsManager(repository, vault);
        var server = KeyServer(@"C:\keys\id_ed25519");

        await manager.SaveAsync(server, null);

        Assert.Null(await vault.GetPasswordAsync(server.Id));
        Assert.Equal(server, await repository.GetAsync(server.Id));
    }

    [Fact]
    public async Task Save_NewPasswordServerWithoutPassword_Throws()
    {
        var manager = new VpsManager(new InMemoryRepository(), new MemoryVault(null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.SaveAsync(VpsServer.Create("VPS", "10.0.0.1", 22, "root"), null));
    }

    [Fact]
    public async Task Save_SwitchingFromPasswordToKey_ClearsStoredPassword()
    {
        var password = VpsServer.Create("VPS", "10.0.0.1", 22, "root");
        var repository = new InMemoryRepository(password);
        var vault = new MemoryVault("clave-vieja");
        var manager = new VpsManager(repository, vault);

        await manager.SaveAsync(password with { AuthMethod = SshAuthMethod.PrivateKey, PrivateKeyPath = @"C:\k" }, null);

        Assert.Null(await vault.GetPasswordAsync(password.Id));
    }

    [Fact]
    public async Task Save_KeepingSameKey_PreservesStoredPassphrase()
    {
        var server = KeyServer(@"C:\k");
        var vault = new MemoryVault("passphrase");
        var manager = new VpsManager(new InMemoryRepository(server), vault);

        await manager.SaveAsync(server with { Name = "Renombrada" }, null);

        Assert.Equal("passphrase", await vault.GetPasswordAsync(server.Id));
    }

    [Fact]
    public async Task Save_SwitchingFromKeyToPassword_RequiresNewPassword()
    {
        var server = KeyServer(@"C:\k");
        var manager = new VpsManager(new InMemoryRepository(server), new MemoryVault("passphrase"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.SaveAsync(server with { AuthMethod = SshAuthMethod.Password, PrivateKeyPath = null }, null));
    }

    private static VpsServer KeyServer(string path) =>
        VpsServer.Create("Clave", "10.0.0.2", 22, "deploy") with
        {
            AuthMethod = SshAuthMethod.PrivateKey,
            PrivateKeyPath = path
        };

    private sealed class InMemoryRepository(params VpsServer[] servers) : IVpsRepository
    {
        private readonly Dictionary<Guid, VpsServer> _items = servers.ToDictionary(item => item.Id);
        public Task<IReadOnlyList<VpsServer>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VpsServer>>([.. _items.Values]);
        public Task<VpsServer?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.GetValueOrDefault(id));
        public Task SaveAsync(VpsServer value, CancellationToken cancellationToken = default) { _items[value.Id] = value; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { _items.Remove(id); return Task.CompletedTask; }
    }

    private sealed class FailingRepository(VpsServer server) : IVpsRepository
    {
        public Task<IReadOnlyList<VpsServer>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VpsServer>>([server]);
        public Task<VpsServer?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<VpsServer?>(server);
        public Task SaveAsync(VpsServer value, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Fallo simulado");
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Fallo simulado");
    }

    private sealed class MemoryVault(string? password) : ICredentialVault
    {
        private string? _password = password;
        public Task StoreAsync(Guid serverId, string username, string value, CancellationToken cancellationToken = default) { _password = value; return Task.CompletedTask; }
        public Task<string?> GetPasswordAsync(Guid serverId, CancellationToken cancellationToken = default) => Task.FromResult(_password);
        public Task DeleteAsync(Guid serverId, CancellationToken cancellationToken = default) { _password = null; return Task.CompletedTask; }
    }
}
