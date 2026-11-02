using FCR.Application;
using FCR.Infrastructure;
using FCR.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FCR.Tests;

/// <summary>Helpers: temp-dir contexts, a copy of the real database, and DB inspection.</summary>
public static class TestDb
{
    /// <summary>Path to the real database in the repo root.</summary>
    public static string RepoDbPath
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "fcr_database.db")))
                dir = dir.Parent;
            return Path.Combine(dir!.FullName, "fcr_database.db");
        }
    }

    /// <summary>Copies the repo database (or WAL sidecars) into a fresh temp dir; returns (dir, options).</summary>
    public static (string Dir, IOptions<FcrOptions> Options) CopyRepoDb()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fcr_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        foreach (var ext in new[] { "", "-wal", "-shm" })
        {
            var src = RepoDbPath + ext;
            if (File.Exists(src)) File.Copy(src, Path.Combine(dir, "fcr_database.db" + ext));
        }
        return (dir, Options(dir));
    }

    public static IOptions<FcrOptions> Options(string dir) =>
        Microsoft.Extensions.Options.Options.Create(new FcrOptions
        {
            DataDir = dir,
            BackupLocation = "backups",
            AttachmentRoot = @"X:\1-DC",
        });

    public static FcrDbContext Context(IOptions<FcrOptions> options) =>
        new(new DbContextOptionsBuilder<FcrDbContext>()
            .UseSqlite(options.Value.ConnectionString)
            .Options);

    /// <summary>Schema fingerprint of a table: (name, type, notnull, pk) per column.</summary>
    public static List<string> TableInfo(SqliteConnection conn, string table)
    {
        var rows = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add($"{r.GetString(1)}|{r.GetString(2)}|notnull={r.GetInt64(3)}|pk={r.GetInt64(5)}");
        return rows;
    }

    public static List<string> IndexList(SqliteConnection conn, string table)
    {
        var rows = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA index_list({table})";
        using var r = cmd.ExecuteReader();
        while (r.Read()) rows.Add(r.GetString(1));
        return rows.OrderBy(x => x).ToList();
    }

    public static SqliteConnection Open(string path)
    {
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        return conn;
    }
}
