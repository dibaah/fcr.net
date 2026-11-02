using FCR.Domain.Entities;

namespace FCR.Application;

/// <summary>FCR persistence contract — implemented by Infrastructure.</summary>
public interface IFcrRepository
{
    Task<List<Fcr>> ListAsync(FcrQuery query, CancellationToken ct = default);
    Task<Fcr?> GetAsync(long id, CancellationToken ct = default);
    Task<Fcr> CreateAsync(FcrRecordInput input, CancellationToken ct = default);
    Task<Fcr> UpdateAsync(long id, FcrRecordInput input, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<BulkSaveResult> BulkSaveAsync(IReadOnlyList<FcrRecordInput> records, CancellationToken ct = default);
    Task<int> BulkDeleteAsync(IReadOnlyList<long> ids, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, long excludeId = 0, CancellationToken ct = default);
}
