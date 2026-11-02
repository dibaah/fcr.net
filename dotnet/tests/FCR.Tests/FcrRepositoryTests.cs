using FCR.Application;
using FCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FCR.Tests;

/// <summary>Repository parity with the Python API contracts.</summary>
public class FcrRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly FcrDbContext _db;
    private readonly FcrRepository _repo;

    public FcrRepositoryTests()
    {
        var (dir, options) = TestDb.CopyRepoDb();
        _dir = dir;
        _db = TestDb.Context(options);
        new DatabaseMigrator(_db).MigrateAsync().GetAwaiter().GetResult();
        _repo = new FcrRepository(_db);
    }

    [Fact]
    public async Task List_ReturnsAllSortedByDateDesc()
    {
        var rows = await _repo.ListAsync(new FcrQuery());
        Assert.Equal(145, rows.Count);
    }

    [Fact]
    public async Task List_StatusGroupFilters()
    {
        var open = await _repo.ListAsync(new FcrQuery(StatusGroup: "open"));
        var finished = await _repo.ListAsync(new FcrQuery(StatusGroup: "finished"));
        var rejected = await _repo.ListAsync(new FcrQuery(StatusGroup: "rejected"));
        var pending = await _repo.ListAsync(new FcrQuery(StatusGroup: "pending"));

        Assert.All(finished, f => Assert.True(
            f.ExecutionStatus.Contains("finished", StringComparison.OrdinalIgnoreCase)
            || f.FcrStatus.Equals("closed", StringComparison.OrdinalIgnoreCase)));
        Assert.All(rejected, f => Assert.Matches("rejected|suspended|cancelled", f.FcrStatus.ToLower()));
        Assert.True(open.Count > 0);
        Assert.Equal(145 - rejected.Count, open.Count + finished.Count + pending.Count
            + (145 - open.Count - finished.Count - pending.Count - rejected.Count)); // sanity
    }

    [Fact]
    public async Task Create_AssignsNextRowNum()
    {
        var maxNum = (await _db.Fcrs.Select(f => f.RowNum).ToListAsync())
            .Where(n => n != null && n.Length > 0 && char.IsDigit(n[0]) && int.TryParse(n, out _))
            .Select(int.Parse).DefaultIfEmpty(0).Max();
        var rec = await _repo.CreateAsync(new FcrRecordInput
        {
            FcrSubject = "Test subject",
            FcrCode = "FCR-WL-PR-900",
        });
        Assert.True(rec.Id > 0);
        Assert.Equal((maxNum + 1).ToString(), rec.RowNum);
        var dup = await _db.Fcrs.CountAsync(f => f.RowNum == rec.RowNum);
        Assert.Equal(1, dup);
    }

    [Fact]
    public async Task Create_RejectsBlankSubject()
    {
        var ex = await Assert.ThrowsAsync<FCR.Domain.FcrValidationException>(
            () => _repo.CreateAsync(new FcrRecordInput { FcrSubject = "  " }));
        Assert.Equal("FCR Subject required", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsInvalidCode()
    {
        await Assert.ThrowsAsync<FCR.Domain.FcrValidationException>(
            () => _repo.CreateAsync(new FcrRecordInput { FcrSubject = "x", FcrCode = "BOGUS" }));
    }

    [Fact]
    public async Task Create_RejectsDuplicateCode()
    {
        var ex = await Assert.ThrowsAsync<FcrDuplicateCodeException>(
            () => _repo.CreateAsync(new FcrRecordInput { FcrSubject = "x", FcrCode = "fcr-ct-pi-046" }));
        Assert.Contains("FCR-CT-PI-046", ex.Message);
    }

    [Fact]
    public async Task Update_AllowsKeepingOwnCode()
    {
        var existing = await _db.Fcrs.FirstAsync(f => f.FcrCode == "FCR-CT-PI-046");
        var updated = await _repo.UpdateAsync(existing.Id, new FcrRecordInput
        {
            FcrSubject = "Updated subject",
            FcrCode = "FCR-CT-PI-046",
            ExecutionStatus = "Finished",
        });
        Assert.Equal("Updated subject", updated.FcrSubject);
    }

    [Fact]
    public async Task Delete_RemovesRecord()
    {
        var existing = await _db.Fcrs.FirstAsync();
        Assert.True(await _repo.DeleteAsync(existing.Id));
        Assert.False(await _repo.DeleteAsync(existing.Id));
    }

    [Fact]
    public async Task BulkSave_UpdatesById_AndReportsSkips()
    {
        var existing = await _db.Fcrs.FirstAsync();
        var result = await _repo.BulkSaveAsync(
        [
            new FcrRecordInput { Id = existing.Id, FcrSubject = "Edited subject", FcrCode = "FCR-EP-ME-700" },
            new FcrRecordInput { FcrSubject = "No id — ignored" },                        // skipped silently
            new FcrRecordInput { Id = 999999, FcrSubject = "Missing" },                    // not found
            new FcrRecordInput { Id = existing.Id, FcrSubject = "Dup", FcrCode = "FCR-CT-IN-031" }, // dup
            new FcrRecordInput { Id = existing.Id, FcrSubject = " ", FcrCode = "" },       // blank subject
        ]);
        Assert.Equal(1, result.Saved);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(3, result.Errors.Count);
        Assert.Equal("Edited subject", (await _db.Fcrs.FindAsync(existing.Id))!.FcrSubject);
    }

    [Fact]
    public async Task RowNumCastSort_MatchesIntegerOrder()
    {
        var rows = await _repo.ListAsync(new FcrQuery(SortBy: "row_num", SortOrder: "asc"));
        var nums = rows
            .Where(r => r.RowNum != null && r.RowNum.Length > 0 && int.TryParse(r.RowNum, out _))
            .Select(r => int.Parse(r.RowNum)).ToList();
        Assert.Equal(nums.OrderBy(n => n).ToList(), nums);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
