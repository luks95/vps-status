using System.Text;
using VPSMonitor.Domain.Monitoring;
using VPSMonitor.Domain.Servers;
using VPSMonitor.Infrastructure.Linux;
using VPSMonitor.Infrastructure.Network;
using VPSMonitor.Infrastructure.Ssh;

namespace VPSMonitor.Infrastructure.Tests;

public sealed class SshServerProbeTests
{
    [Fact]
    public void FormatFingerprint_UsesOpenSshSha256Format()
    {
        var fingerprint = SshServerProbe.FormatFingerprint(Encoding.UTF8.GetBytes("abc"));

        Assert.Equal("SHA256:ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0", fingerprint);
    }

    [Fact]
    public async Task Probe_WithMissingPrivateKey_ReportsAuthenticationFailureWithoutConnecting()
    {
        using var probe = new SshServerProbe(new LinuxSnapshotParser(), new LatencyProbe());
        var server = VpsServer.Create("Clave", "127.0.0.1", 1, "root") with
        {
            AuthMethod = SshAuthMethod.PrivateKey,
            PrivateKeyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "id_ed25519")
        };

        var result = await probe.ProbeAsync(server, string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionStatus.AuthenticationFailed, result.FailureStatus);
        Assert.Contains("clave privada", result.Message);
    }

    [Fact]
    public async Task Probe_WithGarbagePrivateKey_ReportsAuthenticationFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vpsmonitor-{Guid.NewGuid():N}.key");
        await File.WriteAllTextAsync(path, "esto no es una clave");
        try
        {
            using var probe = new SshServerProbe(new LinuxSnapshotParser(), new LatencyProbe());
            var server = VpsServer.Create("Clave", "127.0.0.1", 1, "root") with
            {
                AuthMethod = SshAuthMethod.PrivateKey,
                PrivateKeyPath = path
            };

            var result = await probe.ProbeAsync(server, string.Empty);

            Assert.Equal(ConnectionStatus.AuthenticationFailed, result.FailureStatus);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
