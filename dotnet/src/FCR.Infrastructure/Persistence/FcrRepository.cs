using FCR.Application;
using FCR.Domain;
using FCR.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCR.Infrastructure.Persistence;

/// <summary>
/// FCR data access — exact parity with the Python layer's query rules,
/// cleaning, duplicate-code rejection and auto row_num allocation.
/// </summary>
public sealed class FcrRepository(FcrDbContext db) : IFcrRepository
{
    private static readonly HashSet<string> AllowedSort = new(StringComparer.OrdinalIgnoreCase)
    {
        "date", "fcr_code", "fcr_subject", "category", "originator",
        "action_party", "pedec_status", "fcr_status", "execution_status", "row_num"
    };

    public async Task<List<Fcr>> ListAsync(FcrQuery q, CancellationToken ct = default)
    {
        var items = db.Fcrs.AsQueryable();

        if (!string.IsNullOrEmpty(q.Search))
        {
            var like = $"%{q.Search}%";
            items = items.Where(f =>
                EF.Functions.Like(f.FcrCode, like) || EF.Functions.Like(f.FcrSubject, like) ||
                EF.Functions.Like(f.Originator, like) || EF.Functions.Like(f.ActionParty, like) ||
                EF.Functions.Like(f.Remarks, like) || EF.Functions.Like(f.PedecStatus, like) ||
                EF.Functions.Like(f.FcrStatus, like) || EF.Functions.Like(f.ExecutionStatus, like));
        }
        if (!string.IsNullOrEmpty(q.Category))
            items = items.Where(f => f.Category == q.Category);
        if (!string.IsNullOrEmpty(q.PedecStatus))
            items = items.Where(f => f.PedecStatus == q.PedecStatus);
        if (!string.IsNullOrEmpty(q.FcrStatus))
            items = items.Where(f => f.FcrStatus == q.FcrStatus);
        if (!string.IsNullOrEmpty(q.ExecStatus))
            items = items.Where(f => f.ExecutionStatus == q.ExecStatus);

        if (!string.IsNullOrEmpty(q.StatusGroup))
            items = q.StatusGroup.ToLowerInvariant() switch
            {
                "open" => items.Where(f =>
                    !(EF.Functions.Like(f.ExecutionStatus, "%finished%") || f.FcrStatus.ToLower() == "closed")
                    && !(EF.Functions.Like(f.FcrStatus, "%rejected%")
                        || EF.Functions.Like(f.FcrStatus, "%suspended%")
                        || EF.Functions.Like(f.FcrStatus, "%cancelled%"))),
                "finished" => items.Where(f =>
                    (EF.Functions.Like(f.ExecutionStatus, "%finished%") || f.FcrStatus.ToLower() == "closed")
                    && !(EF.Functions.Like(f.FcrStatus, "%rejected%")
                        || EF.Functions.Like(f.FcrStatus, "%suspended%")
                        || EF.Functions.Like(f.FcrStatus, "%cancelled%"))),
                "pending" => items.Where(f =>
                    (EF.Functions.Like(f.ExecutionStatus, "%pending%")
                        || EF.Functions.Like(f.ExecutionStatus, "%awaiting%")
                        || EF.Functions.Like(f.ExecutionStatus, "%on-going%")
                        || EF.Functions.Like(f.ExecutionStatus, "%preparation%"))
                    && !(EF.Functions.Like(f.FcrStatus, "%rejected%")
                        || EF.Functions.Like(f.FcrStatus, "%suspended%")
                        || EF.Functions.Like(f.FcrStatus, "%cancelled%"))
                    && !(EF.Functions.Like(f.ExecutionStatus, "%finished%") || f.FcrStatus.ToLower() == "closed")),
                "rejected" => items.Where(f =>
                    EF.Functions.Like(f.FcrStatus, "%rejected%")
                    || EF.Functions.Like(f.FcrStatus, "%suspended%")
                    || EF.Functions.Like(f.FcrStatus, "%cancelled%")),
                _ => throw new FcrValidationException(
                    $"Unknown status_group '{q.StatusGroup}'. Allowed: finished, open, pending, rejected."),
            };

        var desc = q.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var sortBy = AllowedSort.Contains(q.SortBy) ? q.SortBy : "date";
        items = sortBy switch
        {
            "date" => desc
                ? items.OrderByDescending(f => f.Date == "" ? 0 : 1).ThenByDescending(f => f.Date)
                : items.OrderBy(f => f.Date == "" ? 1 : 0).ThenBy(f => f.Date),
            "row_num" => desc
                ? items.OrderByDescending(f => f.RowNum.Length).ThenByDescending(f => f.RowNum)
                : items.OrderBy(f => f.RowNum.Length).ThenBy(f => f.RowNum),
            "fcr_code" => desc ? items.OrderByDescending(f => f.FcrCode) : items.OrderBy(f => f.FcrCode),
            "fcr_subject" => desc ? items.OrderByDescending(f => f.FcrSubject) : items.OrderBy(f => f.FcrSubject),
            "category" => desc ? items.OrderByDescending(f => f.Category) : items.OrderBy(f => f.Category),
            "originator" => desc ? items.OrderByDescending(f => f.Originator) : items.OrderBy(f => f.Originator),
            "action_party" => desc ? items.OrderByDescending(f => f.ActionParty) : items.OrderBy(f => f.ActionParty),
            "pedec_status" => desc ? items.OrderByDescending(f => f.PedecStatus) : items.OrderBy(f => f.PedecStatus),
            "fcr_status" => desc ? items.OrderByDescending(f => f.FcrStatus) : items.OrderBy(f => f.FcrStatus),
            "execution_status" => desc ? items.OrderByDescending(f => f.ExecutionStatus) : items.OrderBy(f => f.ExecutionStatus),
            _ => items.OrderByDescending(f => f.Date)
        };

        return await items.AsNoTracking().ToListAsync(ct);
    }

    public Task<Fcr?> GetAsync(long id, CancellationToken ct = default) =>
        db.Fcrs.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<bool> CodeExistsAsync(string code, long excludeId = 0, CancellationToken ct = default)
    {
        var norm = FcrCode.Normalize(code).ToUpperInvariant();
        return db.Fcrs.AnyAsync(f => f.Id != excludeId && f.FcrCode.ToUpper() == norm, ct);
    }

    public async Task<Fcr> CreateAsync(FcrRecordInput input, CancellationToken ct = default)
    {
        RequireSubject(input.FcrSubject);
        var code = FcrCode.Validate(input.FcrCode);
        if (code.Length > 0 && await CodeExistsAsync(code, 0, ct))
            throw new FcrDuplicateCodeException(code);

        var entity = Map(new Fcr(), input);
        entity.FcrCode = code;
        entity.RowNum = await NextRowNumAsync(input.RowNum, ct);
        db.Fcrs.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Fcr> UpdateAsync(long id, FcrRecordInput input, CancellationToken ct = default)
    {
        RequireSubject(input.FcrSubject);
        var entity = await db.Fcrs.FindAsync([id], ct)
            ?? throw new FcrNotFoundException(id);
        var code = FcrCode.Validate(input.FcrCode);
        if (code.Length > 0 && await CodeExistsAsync(code, id, ct))
            throw new FcrDuplicateCodeException(code);

        Map(entity, input);
        entity.FcrCode = code;
        entity.RowNum = ValueCleaning.Clean(input.RowNum);
        entity.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await db.Fcrs.FindAsync([id], ct);
        if (entity is null) return false;
        db.Fcrs.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Bulk UPDATE by id — parity with POST /api/fcr/bulk-save (records without id are ignored).</summary>
    public async Task<BulkSaveResult> BulkSaveAsync(IReadOnlyList<FcrRecordInput> records, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var saved = 0;
        var skipped = 0;

        foreach (var rec in records)
        {
            if (rec.Id is null) continue;
            var entity = await db.Fcrs.FindAsync([rec.Id.Value], ct);
            if (entity is null)
            {
                skipped++;
                errors.Add($"id {rec.Id}: not found");
                continue;
            }
            string code;
            try
            {
                code = FcrCode.Validate(rec.FcrCode);
            }
            catch (FcrValidationException ex)
            {
                skipped++;
                errors.Add($"id {rec.Id}: {ex.Message}");
                continue;
            }
            if (code.Length > 0 && await CodeExistsAsync(code, rec.Id.Value, ct))
            {
                skipped++;
                errors.Add($"id {rec.Id}: duplicate FCR Code {code}");
                continue;
            }
            if (string.IsNullOrWhiteSpace(rec.FcrSubject))
            {
                skipped++;
                errors.Add($"id {rec.Id}: FCR Subject required");
                continue;
            }
            Map(entity, rec);
            entity.FcrCode = code;
            entity.RowNum = ValueCleaning.Clean(rec.RowNum);
            entity.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            saved++;
        }

        await db.SaveChangesAsync(ct);
        return new BulkSaveResult(saved, skipped, errors);
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyList<long> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return 0;
        var deleted = await db.Fcrs.Where(f => ids.Contains(f.Id)).ExecuteDeleteAsync(ct);
        return deleted;
    }

    private static void RequireSubject(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new FcrValidationException("FCR Subject required");
    }

    /// <summary>MAX(CAST(row_num AS INTEGER)) over digit-leading values + 1 — parity with next_row_num().</summary>
    private async Task<string> NextRowNumAsync(string? provided, CancellationToken ct)
    {
        var providedClean = ValueCleaning.Clean(provided);
        if (providedClean.Length > 0) return providedClean;
        var rowNums = await db.Fcrs.Select(f => f.RowNum).ToListAsync(ct);
        return (NextRowNumSeed(rowNums) + 1).ToString();
    }

    private static long NextRowNumSeed(IEnumerable<string> rowNums) =>
        rowNums
            .Where(n => n != null && n.Length > 0 && char.IsDigit(n[0]) && int.TryParse(n, out _))
            .Select(int.Parse)
            .DefaultIfEmpty(0)
            .Max();

    private static Fcr Map(Fcr e, FcrRecordInput i)
    {
        e.FcrSubject = ValueCleaning.Clean(i.FcrSubject);
        e.Category = ValueCleaning.Clean(i.Category);
        e.Date = ValueCleaning.CleanDate(i.Date);
        e.ReviewDate = ValueCleaning.CleanDate(i.ReviewDate);
        e.Originator = ValueCleaning.Clean(i.Originator);
        e.ActionParty = ValueCleaning.Clean(i.ActionParty);
        e.Level = ValueCleaning.Clean(i.Level);
        e.SessionCode = ValueCleaning.Clean(i.SessionCode);
        e.PedecStatus = ValueCleaning.Clean(i.PedecStatus);
        e.FcrStatus = ValueCleaning.Clean(i.FcrStatus);
        e.ExecutionStatus = ValueCleaning.Clean(i.ExecutionStatus);
        e.ClosedDate = ValueCleaning.Clean(i.ClosedDate);
        e.Tr = ValueCleaning.Clean(i.Tr);
        e.NextActionBy = ValueCleaning.Clean(i.NextActionBy);
        e.ArchLoc = ValueCleaning.Clean(i.ArchLoc);
        e.LastUpdate = ValueCleaning.Clean(i.LastUpdate);
        e.Remarks = ValueCleaning.Clean(i.Remarks);
        return e;
    }
}

/// <summary>Duplicate FCR code — maps to HTTP 409.</summary>
public sealed class FcrDuplicateCodeException(string code)
    : Exception($"FCR Code '{code}' already exists")
{
    public string Code { get; } = code;
}

/// <summary>Missing FCR record — maps to HTTP 404.</summary>
public sealed class FcrNotFoundException(long id) : Exception("FCR not found")
{
    public long Id { get; } = id;
}
