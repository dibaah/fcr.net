namespace FCR.Domain.Entities;

/// <summary>Key/value metadata — mirrors the existing `meta` table (e.g. schema_version).</summary>
public class Meta
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
