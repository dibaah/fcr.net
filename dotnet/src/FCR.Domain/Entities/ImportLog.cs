namespace FCR.Domain.Entities;

/// <summary>Import audit record — mirrors the existing `import_log` table.</summary>
public class ImportLog
{
    public long Id { get; set; }
    public string? Filename { get; set; }
    public long? RowCount { get; set; }
    public string ImportedAt { get; set; } = "";
}
