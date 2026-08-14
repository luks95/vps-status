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

    private sealed class FailingRepository(VpsServer server) : IVpsRepository
    {
        public Task<IReadOnlyList<VpsServer>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VpsServer>>([server]);
        public Task<VpsServer?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<VpsServer?>(server);
        public Task SaveAsync(VpsServer value, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Fallo simulado");
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Fallo simulado");
    }

    private sealed class MemoryVault(string password) : ICredentialVault
    {
        private string? _password = password;
        public Task StoreAsync(Guid serverId, string username, string value, CancellationToken cancellationToken = default) { _password = value; return Task.CompletedTask; }
        public Task<string?> GetPasswordAsync(Guid serverId, CancellationToken cancellationToken = default) => Task.FromResult(_password);
        public Task DeleteAsync(Guid serverId, CancellationToken cancellationToken = default) { _password = null; return Task.CompletedTask; }
    }
}
