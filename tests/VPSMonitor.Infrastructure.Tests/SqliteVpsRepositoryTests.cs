using VPSMonitor.Domain.Servers;
using VPSMonitor.Infrastructure.Persistence;

namespace VPSMonitor.Infrastructure.Tests;

public sealed class SqliteVpsRepositoryTests
{
    [Fact]
    public async Task SaveAndGetAll_RoundTripsConfigurationWithoutPasswordColumn()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VPSMonitor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "test.db");

        try
        {
            var repository = new SqliteVpsRepository(databasePath);
            var server = VpsServer.Create("Producción", "192.168.1.10", 22, "root") with
            {
                TrustedHostKeyFingerprint = "SHA256:test",
                ThresholdOverrides = new AlertThresholds(80, 75, 70, 150)
            };

            await repository.SaveAsync(server);
            var restored = Assert.Single(await repository.GetAllAsync());

            Assert.Equal(server, restored);
            var schema = await File.ReadAllBytesAsync(databasePath);
            var schemaText = System.Text.Encoding.UTF8.GetString(schema);
            Assert.DoesNotContain("password", schemaText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
