namespace FCR.Application;

public interface IStatsService
{
    /// <summary>GET /api/stats payload.</summary>
    Dictionary<string, object?> GetStats();

    /// <summary>GET /api/options payload — distinct non-empty values.</summary>
    Dictionary<string, object?> GetOptions();

    /// <summary>GET /api/notifications payload — PEDEC items older than alert_days.</summary>
    Dictionary<string, object?> GetNotifications();
}

/// <summary>Excel export — parity with app/services/export.py.</summary>
public interface IExcelExportService
{
    /// <summary>Workbook bytes for GET /api/export and /api/export-with-charts. null when no records.</summary>
    byte[]? BuildExport(bool withCharts);

    /// <summary>auto-export to file — returns {ok, path?, size_kb?, error?}.</summary>
    Dictionary<string, object?> ExportToFile(string destPath);
}

/// <summary>Folder operations — parity with the folders router.</summary>
public interface IFolderService
{
    string AttachmentRoot { get; }

    /// <summary>Existing dir starting with the code, else derived path, else "".</summary>
    string ResolveFolderPath(string? fcrCode);

    /// <summary>Folder path for list/detail responses (derived, not checked on disk).</summary>
    string GenerateFolderPath(string? fcrCode);
}
