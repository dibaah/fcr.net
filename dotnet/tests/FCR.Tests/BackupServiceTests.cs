using FCR.Infrastructure.Backup;
using FCR.Infrastructure.Settings;
using Xunit;

namespace FCR.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly FcrBackupService _backups;

    public BackupServiceTests()
    {
        var (dir, options) = TestDb.CopyRepoDb();
        _dir = dir;
        _backups = new FcrBackupService(new JsonSettingsStore(options), options);
    }

    [Fact]
    public async Task Create_ThenList_ThenRestore()
    {
        var backup = await _backups.CreateBackupAsync();
        Assert.Equal(true, backup["ok"]);
        Assert.StartsWith("fcr_database_backup_", Path.GetFileName((string)backup["path"]!));
        Assert.True((long)backup["size_kb"]! > 0);

        var list = _backups.ListBackups();
        Assert.Single(list);
        Assert.Equal(Path.GetFileName((string)backup["path"]!), (string)list[0]["filename"]!);

        await _backups.RestoreBackupAsync((string)list[0]["filename"]!); // round-trips without error
    }

    [Fact]
    public async Task Restore_RejectsPathTraversal() =>
        await Assert.ThrowsAsync<FCR.Domain.FcrValidationException>(
            () => _backups.RestoreBackupAsync("../evil.db"));

    [Fact]
    public async Task Restore_RejectsMissing() =>
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _backups.RestoreBackupAsync("fcr_database_backup_19990101_000000.db"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
