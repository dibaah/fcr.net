using FCR.Application;
using FCR.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace FCR.Infrastructure.Backup;

/// <summary>
/// Database backup/restore — parity with app/services/backup.py:
/// timestamped SQLite copies in the settings' backup_location, prune to
/// MaxBackups, newest-first listing.
/// </summary>
public sealed class FcrBackupService(ISettingsStore settings, IOptions<FcrOptions> options)
{
    private readonly FcrOptions _o = options.Value;

    private string BackupDir => settings.ResolveUserPath(settings.Load().BackupLocation, "backups");

    /// <summary>POST /api/backup payload.</summary>
    public async Task<Dictionary<string, object?>> CreateBackupAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(BackupDir);
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var dest = Path.Combine(BackupDir, $"fcr_database_backup_{ts}.db");

        await using var src = new SqliteConnection(_o.ConnectionString);
        await src.OpenAsync(ct);
        await using (var wal = src.CreateCommand())
        {
            wal.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            await wal.ExecuteNonQueryAsync(ct);
        }
        await using var dst = new SqliteConnection($"Data Source={dest}");
        await dst.OpenAsync(ct);
        src.BackupDatabase(dst);

        var pruned = Prune();
        return new Dictionary<string, object?>
        {
            ["ok"] = true, ["path"] = dest,
            ["size_kb"] = new FileInfo(dest).Length / 1024,
            ["timestamp"] = ts, ["pruned"] = pruned,
        };
    }

    /// <summary>GET /api/backups payload — newest first, capped at MaxBackups.</summary>
    public List<Dictionary<string, object?>> ListBackups()
    {
        if (!Directory.Exists(BackupDir)) return [];
        return Directory.EnumerateFiles(BackupDir, "fcr_database_backup_*.db")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(FcrConstants.MaxBackups)
            .Select(f => new Dictionary<string, object?>
            {
                ["filename"] = Path.GetFileName(f),
                ["path"] = f,
                ["size_kb"] = new FileInfo(f).Length / 1024,
                ["created"] = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm"),
            })
            .ToList();
    }

    /// <summary>Restore via the SQLite backup API — replaces the live database transactionally.</summary>
    public async Task RestoreBackupAsync(string filename, CancellationToken ct = default)
    {
        if (filename.Contains('/') || filename.Contains('\\') || filename.Contains(".."))
            throw new FcrValidationException("Invalid backup filename");
        var backupPath = Path.Combine(BackupDir, filename);
        if (!File.Exists(backupPath))
            throw new FileNotFoundException($"Backup '{filename}' not found", filename);

        await using var src = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
        await src.OpenAsync(ct);
        await using var dst = new SqliteConnection(_o.ConnectionString);
        await dst.OpenAsync(ct);
        src.BackupDatabase(dst);
    }

    private int Prune()
    {
        var files = Directory.EnumerateFiles(BackupDir, "fcr_database_backup_*.db")
            .OrderByDescending(f => File.GetLastWriteTime(f)).ToList();
        var removed = 0;
        foreach (var old in files.Skip(FcrConstants.MaxBackups))
        {
            File.Delete(old);
            removed++;
        }
        return removed;
    }
}
