using Microsoft.Data.Sqlite;

namespace FCR.Infrastructure.Persistence;

/// <summary>
/// Port of migrate_db.py — idempotently brings a pre-v2 database up to
/// schema v2 by adding any missing legacy columns. Never removes data.
/// </summary>
public static class LegacySchemaUpgrader
{
    private static readonly (string Column, string Ddl)[] LegacyColumns =
    [
        ("row_num", "TEXT DEFAULT ''"), ("category", "TEXT DEFAULT ''"),
        ("level", "TEXT DEFAULT ''"), ("session_code", "TEXT DEFAULT ''"),
        ("pedec_status", "TEXT DEFAULT ''"), ("fcr_status", "TEXT DEFAULT ''"),
        ("execution_status", "TEXT DEFAULT ''"), ("closed_date", "TEXT DEFAULT ''"),
        ("tr", "TEXT DEFAULT ''"), ("next_action_by", "TEXT DEFAULT ''"),
        ("arch_loc", "TEXT DEFAULT ''"), ("last_update", "TEXT DEFAULT ''"),
        ("remarks", "TEXT DEFAULT ''"),
        ("created_at", "TEXT DEFAULT (datetime('now'))"),
        ("updated_at", "TEXT DEFAULT (datetime('now'))"),
    ];

    /// <summary>Adds missing fcr columns. Returns true when anything changed.</summary>
    public static async Task<bool> UpgradeAsync(SqliteConnection conn, CancellationToken ct = default)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(fcr)";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                existing.Add(reader.GetString(1));
        }
        if (existing.Count == 0) return false; // no fcr table — nothing to upgrade

        var changed = false;
        foreach (var (column, ddl) in LegacyColumns)
        {
            if (existing.Contains(column)) continue;
            await using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE fcr ADD COLUMN {column} {ddl}";
            await alter.ExecuteNonQueryAsync(ct);
            changed = true;
        }
        return changed;
    }
}
