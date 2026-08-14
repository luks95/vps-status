using System.Globalization;
using Microsoft.Data.Sqlite;
using VPSMonitor.Application.Abstractions;
using VPSMonitor.Domain.Servers;

namespace VPSMonitor.Infrastructure.Persistence;

public sealed class SqliteVpsRepository : IVpsRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public SqliteVpsRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
    }

    public async Task<IReadOnlyList<VpsServer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, host, port, username, is_enabled, host_fingerprint,
                   cpu_threshold, memory_threshold, disk_threshold, latency_threshold
            FROM vps_servers
            ORDER BY name COLLATE NOCASE;
            """;

        var result = new List<VpsServer>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadServer(reader));
        return result;
    }

    public async Task<VpsServer?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, host, port, username, is_enabled, host_fingerprint,
                   cpu_threshold, memory_threshold, disk_threshold, latency_threshold
            FROM vps_servers WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadServer(reader) : null;
    }

    public async Task SaveAsync(VpsServer server, CancellationToken cancellationToken = default)
    {
        server.Validate();
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO vps_servers (
                id, name, host, port, username, is_enabled, host_fingerprint,
                cpu_threshold, memory_threshold, disk_threshold, latency_threshold)
            VALUES (
                $id, $name, $host, $port, $username, $enabled, $fingerprint,
                $cpu, $memory, $disk, $latency)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                host = excluded.host,
                port = excluded.port,
                username = excluded.username,
                is_enabled = excluded.is_enabled,
                host_fingerprint = excluded.host_fingerprint,
                cpu_threshold = excluded.cpu_threshold,
                memory_threshold = excluded.memory_threshold,
                disk_threshold = excluded.disk_threshold,
                latency_threshold = excluded.latency_threshold;
            """;
        command.Parameters.AddWithValue("$id", server.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", server.Name);
        command.Parameters.AddWithValue("$host", server.Host);
        command.Parameters.AddWithValue("$port", server.Port);
        command.Parameters.AddWithValue("$username", server.Username);
        command.Parameters.AddWithValue("$enabled", server.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$fingerprint", (object?)server.TrustedHostKeyFingerprint ?? DBNull.Value);
        command.Parameters.AddWithValue("$cpu", (object?)server.ThresholdOverrides?.CpuPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("$memory", (object?)server.ThresholdOverrides?.MemoryPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("$disk", (object?)server.ThresholdOverrides?.DiskPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("$latency", (object?)server.ThresholdOverrides?.LatencyMilliseconds ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM vps_servers WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS vps_servers (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    host TEXT NOT NULL,
                    port INTEGER NOT NULL CHECK (port BETWEEN 1 AND 65535),
                    username TEXT NOT NULL,
                    is_enabled INTEGER NOT NULL DEFAULT 1,
                    host_fingerprint TEXT NULL,
                    cpu_threshold REAL NULL,
                    memory_threshold REAL NULL,
                    disk_threshold REAL NULL,
                    latency_threshold REAL NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static VpsServer ReadServer(SqliteDataReader reader)
    {
        var hasOverrides = !reader.IsDBNull(7) || !reader.IsDBNull(8) || !reader.IsDBNull(9) || !reader.IsDBNull(10);
        var thresholds = hasOverrides
            ? new AlertThresholds(
                ReadDouble(reader, 7, 90),
                ReadDouble(reader, 8, 85),
                ReadDouble(reader, 9, 80),
                ReadDouble(reader, 10, 200))
            : null;

        return new VpsServer(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetInt32(5) == 1,
            reader.IsDBNull(6) ? null : reader.GetString(6),
            thresholds);
    }

    private static double ReadDouble(SqliteDataReader reader, int ordinal, double fallback) =>
        reader.IsDBNull(ordinal)
            ? fallback
            : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
