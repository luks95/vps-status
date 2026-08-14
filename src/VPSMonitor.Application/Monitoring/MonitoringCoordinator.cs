using System.Collections.Concurrent;
using VPSMonitor.Application.Abstractions;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Application.Monitoring;

public sealed class MonitoringCoordinator(
    IServerProbe serverProbe,
    ICredentialVault credentialVault) : IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly int[] RetrySeconds = [2, 5, 10, 20, 30];
    private readonly ConcurrentDictionary<Guid, WorkerRegistration> _workers = [];

    public event Action<ServerMonitorState>? StateChanged;

    public void Start(VpsServer server)
    {
        if (!server.IsEnabled || _workers.ContainsKey(server.Id)) return;
        var cancellation = new CancellationTokenSource();
        var registration = new WorkerRegistration(cancellation);
        if (_workers.TryAdd(server.Id, registration))
        {
            registration.Task = RunWorkerAsync(server, registration, cancellation.Token);
        }
        else
        {
            cancellation.Dispose();
        }
    }

    public void Start(IEnumerable<VpsServer> servers)
    {
        foreach (var server in servers.Take(5)) Start(server);
    }

    public async Task StopAsync(Guid serverId)
    {
        if (!_workers.TryRemove(serverId, out var registration)) return;
        await registration.Cancellation.CancelAsync();
        try
        {
            await registration.Task;
        }
        catch (OperationCanceledException)
        {
            // El estado de cancelación ya fue publicado por el worker.
        }
        registration.Cancellation.Dispose();
    }

    public async Task RestartAsync(VpsServer server)
    {
        await StopAsync(server.Id);
        Start(server);
    }

    private async Task RunWorkerAsync(
        VpsServer server,
        WorkerRegistration registration,
        CancellationToken cancellationToken)
    {
        RawSystemSnapshot? previous = null;
        var failureCount = 0;
        Publish(server.Id, ConnectionStatus.Connecting, null, "Conectando…");

        try
        {
            var password = await credentialVault.GetPasswordAsync(server.Id, cancellationToken);
            if (password is null)
            {
                Publish(server.Id, ConnectionStatus.AuthenticationFailed, null, "No se encontró la contraseña guardada.");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var startedAt = TimeProvider.System.GetTimestamp();
                var result = await serverProbe.ProbeAsync(server, password, cancellationToken);

                if (result.IsSuccess && result.Snapshot is not null)
                {
                    var metrics = MetricCalculator.Calculate(
                        previous,
                        result.Snapshot,
                        result.LatencyMilliseconds,
                        result.LatencyMethod);
                    previous = result.Snapshot;
                    failureCount = 0;
                    Publish(server.Id, ConnectionStatus.Online, metrics, null);

                    var elapsed = TimeProvider.System.GetElapsedTime(startedAt);
                    var delay = PollInterval - elapsed;
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
                    continue;
                }

                failureCount++;
                previous = null;
                var status = result.FailureStatus is ConnectionStatus.HostKeyChanged
                    or ConnectionStatus.HostKeyUnknown
                    or ConnectionStatus.AuthenticationFailed
                    or ConnectionStatus.Warning
                    ? result.FailureStatus
                    : ConnectionStatus.Reconnecting;
                Publish(server.Id, status, null, result.Message);

                if (status is ConnectionStatus.HostKeyChanged or ConnectionStatus.HostKeyUnknown or ConnectionStatus.AuthenticationFailed)
                {
                    return;
                }

                var retryIndex = Math.Min(failureCount - 1, RetrySeconds.Length - 1);
                await Task.Delay(TimeSpan.FromSeconds(RetrySeconds[retryIndex]), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Publish(server.Id, ConnectionStatus.Disabled, null, "Monitoreo detenido.");
        }
        catch (Exception exception)
        {
            Publish(server.Id, ConnectionStatus.Offline, null, $"Error de monitoreo: {exception.Message}");
        }
        finally
        {
            serverProbe.Close(server.Id);
            if (_workers.TryGetValue(server.Id, out var current) && ReferenceEquals(current, registration))
            {
                if (_workers.TryRemove(server.Id, out _)) registration.Cancellation.Dispose();
            }
        }
    }

    private void Publish(Guid serverId, ConnectionStatus status, SystemMetrics? metrics, string? message) =>
        StateChanged?.Invoke(new ServerMonitorState(serverId, status, metrics, message, DateTimeOffset.UtcNow));

    public async ValueTask DisposeAsync()
    {
        var workers = _workers.ToArray();
        foreach (var worker in workers) _workers.TryRemove(worker.Key, out _);
        foreach (var worker in workers) await worker.Value.Cancellation.CancelAsync();
        await Task.WhenAll(workers.Select(worker => worker.Value.Task));
        foreach (var worker in workers) worker.Value.Cancellation.Dispose();
        _workers.Clear();
    }

    private sealed class WorkerRegistration(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public Task Task { get; set; } = Task.CompletedTask;
    }
}
