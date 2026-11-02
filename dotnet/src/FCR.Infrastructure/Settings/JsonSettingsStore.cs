using System.Text.Json;
using System.Text.Json.Nodes;
using FCR.Application;
using FCR.Domain;
using Microsoft.Extensions.Options;

namespace FCR.Infrastructure.Settings;

/// <summary>
/// settings.json store — same file, defaults, merge and write semantics
/// as app/services/settings.py (atomic tmp+replace, readonly keys stripped).
/// </summary>
public sealed class JsonSettingsStore(IOptions<FcrOptions> options) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static readonly HashSet<string> ReadonlyKeys =
    [
        "db_location", "app_version", "backup_resolved",
        "auto_export_resolved", "auto_backup_resolved", "schema_version",
    ];

    private readonly FcrOptions _o = options.Value;
    private string SettingsPath => Path.Combine(_o.DataDir, "settings.json");

    public FcrSettings Load()
    {
        var (loaded, existed) = LoadRaw();
        // merge dept_codes with defaults (missing keys fall back)
        var merged = new Dictionary<string, string>(FcrConstants.DefaultDeptCodes);
        foreach (var kv in loaded.DeptCodes) merged[kv.Key] = kv.Value;
        loaded.DeptCodes = merged;
        // Fcr:AttachmentRoot config is the deployment-time default for a
        // centralized share; an explicit settings.json value always wins.
        if (!existed || string.IsNullOrWhiteSpace(loaded.AttachmentRoot))
            loaded.AttachmentRoot = _o.AttachmentRoot;
        return loaded;
    }

    private (FcrSettings, bool Existed) LoadRaw()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                return (JsonSerializer.Deserialize<FcrSettings>(
                    File.ReadAllText(SettingsPath)) ?? new FcrSettings(), true);
            }
        }
        catch { /* unreadable settings.json → defaults, same as Python */ }
        return (new FcrSettings(), false);
    }

    public void Save(FcrSettings settings)
    {
        var json = JsonSerializer.SerializeToNode(settings, JsonOpts)!.AsObject();
        foreach (var key in ReadonlyKeys) json.Remove(key);
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(json, JsonOpts));
        File.Move(tmp, SettingsPath, overwrite: true);
    }

    public void Reset()
    {
        if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
        Save(new FcrSettings());
    }

    public string ResolveUserPath(string? value, string defaultName)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? defaultName : value.Trim();
        return Path.IsPathRooted(raw) ? raw : Path.Combine(_o.DataDir, raw);
    }

    public Dictionary<string, object?> Payload()
    {
        var s = Load();
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            JsonSerializer.Serialize(s, JsonOpts)) ?? new();
        dict["app_version"] = FcrConstants.AppVersion;
        dict["db_location"] = _o.DbPath;
        dict["backup_resolved"] = ResolveUserPath(s.BackupLocation, "backups");
        dict["auto_export_resolved"] = ResolveUserPath(s.AutoExportLocation, "exports");
        dict["schema_version"] = FcrConstants.SchemaVersion;
        return dict;
    }
}
