using FCR.Infrastructure;
using FCR.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FCR.Tests;

/// <summary>
/// Master plan Phase 1 item 10: verify schema compatibility — an EF-migrated
/// database must reproduce the existing database's schema exactly, and the
/// migrator must leave a real DB copy untouched.
/// </summary>
public class SchemaCompatibilityTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    private (string dir, string dbPath) TempDbDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fcr_schema_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return (dir, Path.Combine(dir, "fcr_database.db"));
    }

    [Fact]
    public async Task FreshMigration_ReproducesExistingSchema()
    {
        var (_, dbPath) = TempDbDir();
        var options = TestDb.Options(Path.GetDirectoryName(dbPath)!);
        await using var db = TestDb.Context(options);
        await db.Database.MigrateAsync();

        await using var newConn = TestDb.Open(dbPath);
        await using var refConn = TestDb.Open(TestDb.RepoDbPath);

        foreach (var table in new[] { "fcr", "import_log", "meta" })
        {
            var expected = TestDb.TableInfo(refConn, table);
            var actual = TestDb.TableInfo(newConn, table);
            Assert.Equal(expected, actual);
        }

        var expectedIndexes = TestDb.IndexList(refConn, "fcr")
            .Where(i => !i.StartsWith("sqlite_autoindex")).OrderBy(x => x);
        var actualIndexes = TestDb.IndexList(newConn, "fcr")
            .Where(i => !i.StartsWith("sqlite_autoindex")).OrderBy(x => x);
        Assert.Equal(expectedIndexes, actualIndexes);
    }

    [Fact]
    public async Task Migrator_PreservesExistingDataAndSchema()
    {
        var (dir, options) = TestDb.CopyRepoDb();
        _tempDirs.Add(dir);

        int rowCountBefore;
        await using (var before = TestDb.Open(Path.Combine(dir, "fcr_database.db")))
        {
            await using var cmd = before.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM fcr";
            rowCountBefore = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        await using var db = TestDb.Context(options);
        await new DatabaseMigrator(db).MigrateAsync();

        await using var after = TestDb.Open(Path.Combine(dir, "fcr_database.db"));
        await using var count = after.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM fcr";
        Assert.Equal(rowCountBefore, Convert.ToInt32(await count.ExecuteScalarAsync()));

        await using var ver = after.CreateCommand();
        ver.CommandText = "SELECT value FROM meta WHERE key='schema_version'";
        Assert.Equal("2", (string)(await ver.ExecuteScalarAsync())!);

        // EF history baselined → migrator is idempotent and MigrateAsync() finds nothing pending.
        var pending = await db.Database.GetPendingMigrationsAsync();
        Assert.Empty(pending);
    }

    [Fact]
    public async Task ExistingDb_IsQueryableThroughEf()
    {
        var (dir, options) = TestDb.CopyRepoDb();
        _tempDirs.Add(dir);
        await using var db = TestDb.Context(options);
        await new DatabaseMigrator(db).MigrateAsync();

        var rows = await db.Fcrs.AsNoTracking().ToListAsync();
        Assert.Equal(145, rows.Count);
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.FcrSubject)));

        var dup = rows.Where(r => r.FcrCode == "FCR-CT-IN-031").ToList();
        Assert.Equal(2, dup.Count);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
            try { Directory.Delete(dir, recursive: true); } catch { /* temp cleanup best-effort */ }
        SqliteConnection.ClearAllPools();
    }
}
