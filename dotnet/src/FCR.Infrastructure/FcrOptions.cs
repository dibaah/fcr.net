using System.Security.Claims;

namespace FCR.Infrastructure;

/// <summary>Filesystem-backed options — parity with settings.json paths.</summary>
public sealed class FcrOptions
{
    /// <summary>Data directory holding fcr_database.db (FCR_DATA_DIR equivalent).</summary>
    public string DataDir { get; set; } = ".";

    /// <summary>Settings-only: where backups are written (relative → under DataDir).</summary>
    public string BackupLocation { get; set; } = "backups";

    /// <summary>Settings-only: network root for FCR attachment folders (X:\1-DC equivalent).</summary>
    public string AttachmentRoot { get; set; } = @"X:\1-DC";
    /// <summary>Centralized store for request attachments; default {DataDir}/request-attachments.</summary>
    public string RequestAttachmentDir { get; set; } = "";

    /// <summary>Claim carrying the stable ERMS subject key mapped to FCR users.external_id.</summary>
    public string IdentityExternalIdClaim { get; set; } = ClaimTypes.NameIdentifier;

    public string DbPath => Path.Combine(DataDir, "fcr_database.db");

    public string ConnectionString => $"Data Source={DbPath}";

    public string ResolveUserPath(string? value, string defaultName)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? defaultName : value.Trim();
        return Path.IsPathRooted(raw) ? raw : Path.Combine(DataDir, raw);
    }
}
