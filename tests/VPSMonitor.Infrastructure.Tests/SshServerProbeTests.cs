using System.Text;
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
}
