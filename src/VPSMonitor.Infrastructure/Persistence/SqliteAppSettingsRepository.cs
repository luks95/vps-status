using Microsoft.Data.Sqlite;
using VPSMonitor.Application.Abstractions;
using VPSMonitor.Domain.Servers;
using VPSMonitor.Domain.Settings;

namespace VPSMonitor.Infrastructure.Persistence;

public sealed class SqliteAppSettingsRepository(string databasePath) : IAppSettingsRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = false
    }.ToString();

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT cpu, memory, disk, latency, tray_hint FROM app_settings WHERE id = 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return AppSettings.Default;
        return new AppSettings(
            new AlertThresholds(reader.GetDouble(0), reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3)),
            reader.GetInt32(4) == 1);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_settings (id, cpu, memory, disk, latency, tray_hint)
            VALUES (1, $cpu, $memory, $disk, $latency, $tray)
            ON CONFLICT(id) DO UPDATE SET
                cpu = excluded.cpu,
                memory = excluded.memory,
                disk = excluded.disk,
                latency = excluded.latency,
                tray_hint = excluded.tray_hint;
            """;
        command.Parameters.AddWithValue("$cpu", settings.GlobalThresholds.CpuPercent);
        command.Parameters.AddWithValue("$memory", settings.GlobalThresholds.MemoryPercent);
        command.Parameters.AddWithValue("$disk", settings.GlobalThresholds.DiskPercent);
        command.Parameters.AddWithValue("$latency", settings.GlobalThresholds.LatencyMilliseconds);
        command.Parameters.AddWithValue("$tray", settings.HasShownTrayHint ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(new SqliteConnectionStringBuilder(_connectionString).DataSource);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS app_settings (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                cpu REAL NOT NULL,
                memory REAL NOT NULL,
                disk REAL NOT NULL,
                latency REAL NOT NULL,
                tray_hint INTEGER NOT NULL DEFAULT 0
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
