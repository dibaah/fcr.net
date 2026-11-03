namespace FCR.Domain.Entities;

/// <summary>
/// Append-only application audit event. Stores metadata only; never request bodies,
/// passwords, attachment contents, or other secrets.
/// </summary>
public sealed class FcrAuditLog
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = "";
    public string HttpMethod { get; set; } = "";
    public string Path { get; set; } = "";
    public int StatusCode { get; set; }
    public string? RequestId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
