using FCR.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FCR.Infrastructure.Persistence;

/// <summary>
/// Brings any database to the current schema:
///   fresh DB  → EF migrations create everything;
///   existing v2 DB → baselines the EF history table so migrations
///                    continue from there without touching data;
///   pre-v2 DB → legacy column upgrade first, then baseline.
/// Always stamps meta.schema_version = 2. Never deletes data.
/// </summary>
public sealed class DatabaseMigrator(FcrDbContext db)
{
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);
        try
        {
            await conn.ExecuteAsync("PRAGMA journal_mode=WAL", ct);
            await conn.ExecuteAsync("PRAGMA foreign_keys=ON", ct);

            var fcrExists = await TableExistsAsync(conn, "fcr", ct);
            if (fcrExists)
            {
                await LegacySchemaUpgrader.UpgradeAsync(conn, ct);
                await BaselineMigrationsAsync(conn, ct);
            }
            await db.Database.MigrateAsync(ct);
            await StampSchemaVersionAsync(conn, ct);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection conn, string table, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
        cmd.Parameters.AddWithValue("$name", table);
        return (long)(await cmd.ExecuteScalarAsync(ct))! > 0;
    }

    /// <summary>
    /// Marks only the first EF migration as applied — a pre-existing database's
    /// schema matches it exactly; later migrations still run via MigrateAsync.
    /// </summary>
    private async Task BaselineMigrationsAsync(SqliteConnection conn, CancellationToken ct)
    {
        await conn.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (" +
            "MigrationId TEXT PRIMARY KEY, ProductVersion TEXT NOT NULL)", ct);
        await conn.ExecuteAsync(
            "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) " +
            "VALUES ($id, $ver)", ct,
            ("$id", db.Database.GetMigrations().First()), ("$ver", "8.0.0"));
    }

    private async Task StampSchemaVersionAsync(SqliteConnection conn, CancellationToken ct)
    {
        await conn.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT)", ct);
        await conn.ExecuteAsync(
            "INSERT INTO meta(key, value) VALUES('schema_version', $v) " +
            "ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            ct, ("$v", FcrConstants.SchemaVersion.ToString()));
    }
}

internal static class SqliteCommandExtensions
{
    public static async Task ExecuteAsync(this SqliteConnection conn, string sql,
        CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
