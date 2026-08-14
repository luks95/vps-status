using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Domain.Tests;

public sealed class VpsServerTests
{
    [Fact]
    public void Create_TrimsValuesAndUsesDefaultEnabledState()
    {
        var server = VpsServer.Create(" Producción ", " 10.0.0.1 ", 22, " root ");

        server.Validate();
        Assert.Equal("Producción", server.Name);
        Assert.Equal("10.0.0.1", server.Host);
        Assert.Equal("root", server.Username);
        Assert.True(server.IsEnabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_RejectsInvalidPort(int port)
    {
        var server = VpsServer.Create("VPS", "10.0.0.1", port, "root");

        Assert.Throws<ArgumentOutOfRangeException>(server.Validate);
    }
}
