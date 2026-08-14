using VPSMonitor.Application.Abstractions;
using VPSMonitor.Application.Monitoring;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Tests;

public sealed class MonitoringCoordinatorTests
{
    [Fact]
    public async Task AuthenticationFailure_IsPublishedOnceAndClosesProbe()
    {
        var server = VpsServer.Create("VPS", "10.0.0.1", 22, "root");
        var probe = new AuthenticationFailureProbe();
        await using var coordinator = new MonitoringCoordinator(probe, new FixedVault());
        var completion = new TaskCompletionSource<ServerMonitorState>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.StateChanged += state =>
        {
            if (state.Status == ConnectionStatus.AuthenticationFailed) completion.TrySetResult(state);
        };

        coordinator.Start(server);
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.Equal(1, probe.CallCount);
        Assert.Equal(1, probe.CloseCount);
    }

    private sealed class AuthenticationFailureProbe : IServerProbe
    {
        public int CallCount { get; private set; }
        public int CloseCount { get; private set; }

        public Task<ProbeResult> ProbeAsync(VpsServer server, string password, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(ProbeResult.Failure(ConnectionStatus.AuthenticationFailed, "Credenciales incorrectas"));
        }

        public void Close(Guid serverId) => CloseCount++;
    }

    private sealed class FixedVault : ICredentialVault
    {
        public Task StoreAsync(Guid serverId, string username, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetPasswordAsync(Guid serverId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("secret");
        public Task DeleteAsync(Guid serverId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
