using System.Text.Json.Serialization;

namespace FCR.Application;

/// <summary>settings.json shape — parity with DEFAULT_SETTINGS.</summary>
public sealed class FcrSettings
{
    [JsonPropertyName("company_name")] public string CompanyName { get; set; } = "SINOPEC International — YADAVARAN";
    [JsonPropertyName("attachment_root")] public string AttachmentRoot { get; set; } = @"X:\1-DC";
    [JsonPropertyName("backup_location")] public string BackupLocation { get; set; } = "backups";
    [JsonPropertyName("auto_backup_enabled")] public bool AutoBackupEnabled { get; set; } = true;
    [JsonPropertyName("auto_backup_interval_days")] public int AutoBackupIntervalDays { get; set; } = 1;
    [JsonPropertyName("last_auto_backup")] public string LastAutoBackup { get; set; } = "";
    [JsonPropertyName("auto_export_enabled")] public bool AutoExportEnabled { get; set; }
    [JsonPropertyName("auto_export_location")] public string AutoExportLocation { get; set; } = "exports";
    [JsonPropertyName("auto_export_interval_days")] public int AutoExportIntervalDays { get; set; } = 2;
    [JsonPropertyName("last_auto_export")] public string LastAutoExport { get; set; } = "";
    [JsonPropertyName("pedec_alert_days")] public int PedecAlertDays { get; set; } = 14;
    [JsonPropertyName("dark_mode")] public bool DarkMode { get; set; }
    [JsonPropertyName("language")] public string Language { get; set; } = "en";
    [JsonPropertyName("dept_codes")] public Dictionary<string, string> DeptCodes { get; set; } = new(FCR.Domain.FcrConstants.DefaultDeptCodes);

    /// <summary>Preserves any extra keys the Python app may have written.</summary>
    [JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
}

public interface ISettingsStore
{
    FcrSettings Load();
    void Save(FcrSettings settings);
    void Reset();

    /// <summary>settings_payload() — settings + app_version/db_location/resolved paths/schema_version.</summary>
    Dictionary<string, object?> Payload();
    string ResolveUserPath(string? value, string defaultName);
}
