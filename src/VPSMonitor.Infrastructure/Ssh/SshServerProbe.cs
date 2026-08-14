using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using VPSMonitor.Application.Abstractions;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;
using VPSMonitor.Infrastructure.Linux;
using VPSMonitor.Infrastructure.Network;

namespace VPSMonitor.Infrastructure.Ssh;

public sealed class SshServerProbe(
    LinuxSnapshotParser snapshotParser,
    LatencyProbe latencyProbe) : IServerProbe, IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(4);
    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = [];
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sessionLocks = [];

    public async Task<ProbeResult> ProbeAsync(
        VpsServer server,
        string password,
        CancellationToken cancellationToken = default)
    {
        var gate = _sessionLocks.GetOrAdd(server.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        SshClient? client = null;
        string? observedFingerprint = null;
        try
        {
            var latencyTask = latencyProbe.MeasureAsync(server.Host, server.Port, cancellationToken);
            var passwordHash = HashSecret(password);
            try
            {
                if (_sessions.TryGetValue(server.Id, out var existing) && existing.Matches(server, passwordHash))
                {
                    client = existing.Client;
                }
                else
                {
                    RemoveSession(server.Id);
                    client = CreateClient(server, password, fingerprint => observedFingerprint = fingerprint);
                    using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    connectTimeout.CancelAfter(ConnectTimeout);
                    await client.ConnectAsync(connectTimeout.Token);
                    var entry = new SessionEntry(server, passwordHash, client);
                    passwordHash = [];
                    _sessions[server.Id] = entry;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordHash);
            }

            using var command = client.CreateCommand(LinuxMetricScript.Value);
            command.CommandTimeout = CommandTimeout;
            using var commandTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            commandTimeout.CancelAfter(CommandTimeout);
            await command.ExecuteAsync(commandTimeout.Token);
            var output = command.Result;
            if (command.ExitStatus is not (null or 0) && string.IsNullOrWhiteSpace(output))
            {
                return ProbeResult.Failure(ConnectionStatus.Offline, "La VPS rechazó la consulta de métricas.");
            }

            var snapshot = snapshotParser.Parse(output, DateTimeOffset.UtcNow);
            var latency = await latencyTask;
            return ProbeResult.Success(
                snapshot,
                latency.Milliseconds,
                latency.Method,
                observedFingerprint ?? server.TrustedHostKeyFingerprint ?? string.Empty);
        }
        catch (SshAuthenticationException)
        {
            RemoveSession(server.Id, client);
            return ProbeResult.Failure(ConnectionStatus.AuthenticationFailed, "Usuario o contraseña incorrectos.");
        }
        catch (SshConnectionException) when (observedFingerprint is not null && server.TrustedHostKeyFingerprint is null)
        {
            RemoveSession(server.Id, client);
            return ProbeResult.Failure(
                ConnectionStatus.HostKeyUnknown,
                "La huella SSH todavía no fue aprobada.",
                observedFingerprint);
        }
        catch (SshConnectionException) when (observedFingerprint is not null)
        {
            RemoveSession(server.Id, client);
            return ProbeResult.Failure(
                ConnectionStatus.HostKeyChanged,
                "La huella SSH cambió. La conexión fue bloqueada.",
                observedFingerprint);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RemoveSession(server.Id, client);
            throw;
        }
        catch (OperationCanceledException)
        {
            RemoveSession(server.Id, client);
            return ProbeResult.Failure(ConnectionStatus.Offline, "La conexión agotó el tiempo de espera.");
        }
        catch (Exception exception) when (exception is SshException or SocketException or TimeoutException)
        {
            RemoveSession(server.Id, client);
            return ProbeResult.Failure(ConnectionStatus.Offline, FriendlyMessage(exception));
        }
        catch (FormatException exception)
        {
            return ProbeResult.Failure(ConnectionStatus.Warning, $"No se pudieron interpretar las métricas: {exception.Message}");
        }
        finally
        {
            gate.Release();
        }
    }

    public static string FormatFingerprint(byte[] hostKey)
    {
        var hash = SHA256.HashData(hostKey);
        return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
    }

    public void Close(Guid serverId) => RemoveSession(serverId);

    public void Dispose()
    {
        foreach (var id in _sessions.Keys) RemoveSession(id);
        foreach (var gate in _sessionLocks.Values) gate.Dispose();
        _sessionLocks.Clear();
    }

    private static SshClient CreateClient(VpsServer server, string password, Action<string> onFingerprint)
    {
        var authentication = new PasswordAuthenticationMethod(server.Username, password);
        var connectionInfo = new ConnectionInfo(server.Host, server.Port, server.Username, authentication)
        {
            Timeout = ConnectTimeout
        };
        var client = new SshClient(connectionInfo);
        client.HostKeyReceived += (_, eventArgs) =>
        {
            var fingerprint = FormatFingerprint(eventArgs.HostKey);
            onFingerprint(fingerprint);
            eventArgs.CanTrust = string.Equals(
                fingerprint,
                server.TrustedHostKeyFingerprint,
                StringComparison.Ordinal);
        };
        return client;
    }

    private void RemoveSession(Guid serverId, SshClient? expectedClient = null)
    {
        if (!_sessions.TryGetValue(serverId, out var entry))
        {
            expectedClient?.Dispose();
            return;
        }
        if (expectedClient is not null && !ReferenceEquals(entry.Client, expectedClient))
        {
            expectedClient.Dispose();
            return;
        }
        if (_sessions.TryRemove(serverId, out var removed)) removed.Dispose();
    }

    private static byte[] HashSecret(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string FriendlyMessage(Exception exception) => exception switch
    {
        TimeoutException => "La conexión agotó el tiempo de espera.",
        SocketException => "No fue posible alcanzar la dirección o el puerto SSH.",
        _ => "La conexión SSH se interrumpió."
    };

    private sealed class SessionEntry : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string? _fingerprint;
        private readonly byte[] _passwordHash;

        public SessionEntry(VpsServer server, byte[] passwordHash, SshClient client)
        {
            _host = server.Host;
            _port = server.Port;
            _username = server.Username;
            _fingerprint = server.TrustedHostKeyFingerprint;
            _passwordHash = passwordHash;
            Client = client;
        }

        public SshClient Client { get; }

        public bool Matches(VpsServer server, byte[] passwordHash) =>
            Client.IsConnected &&
            string.Equals(_host, server.Host, StringComparison.OrdinalIgnoreCase) &&
            _port == server.Port &&
            string.Equals(_username, server.Username, StringComparison.Ordinal) &&
            string.Equals(_fingerprint, server.TrustedHostKeyFingerprint, StringComparison.Ordinal) &&
            CryptographicOperations.FixedTimeEquals(_passwordHash, passwordHash);

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(_passwordHash);
            if (Client.IsConnected) Client.Disconnect();
            Client.Dispose();
        }
    }
}
