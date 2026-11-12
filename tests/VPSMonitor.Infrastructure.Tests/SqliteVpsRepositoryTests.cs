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

    [Fact]
    public async Task SaveAndGetAll_RoundTripsPrivateKeyAuthentication()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VPSMonitor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var repository = new SqliteVpsRepository(Path.Combine(directory, "test.db"));
            var server = VpsServer.Create("Clave", "192.168.1.11", 22, "deploy") with
            {
                AuthMethod = SshAuthMethod.PrivateKey,
                PrivateKeyPath = @"C:\Users\me\.ssh\id_ed25519"
            };

            await repository.SaveAsync(server);

            Assert.Equal(server, Assert.Single(await repository.GetAllAsync()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Initialize_MigratesDatabaseCreatedBeforeKeyAuthentication()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VPSMonitor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "old.db");

        try
        {
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE vps_servers (
                        id TEXT PRIMARY KEY, name TEXT NOT NULL, host TEXT NOT NULL,
                        port INTEGER NOT NULL, username TEXT NOT NULL, is_enabled INTEGER NOT NULL DEFAULT 1,
                        host_fingerprint TEXT NULL, cpu_threshold REAL NULL, memory_threshold REAL NULL,
                        disk_threshold REAL NULL, latency_threshold REAL NULL);
                    INSERT INTO vps_servers (id, name, host, port, username)
                    VALUES ('7d3f7c43-45a8-4c8d-a2a1-1f5f6f0f9a10', 'Antigua', '10.0.0.5', 22, 'root');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var restored = Assert.Single(await new SqliteVpsRepository(databasePath).GetAllAsync());

            Assert.Equal(SshAuthMethod.Password, restored.AuthMethod);
            Assert.Null(restored.PrivateKeyPath);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}
