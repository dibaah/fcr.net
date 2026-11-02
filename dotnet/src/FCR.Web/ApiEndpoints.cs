using System.Text.Json;
using System.Text.Json.Nodes;
using FCR.Application;
using FCR.Domain;
using FCR.Infrastructure;
using FCR.Infrastructure.Backup;
using FCR.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace FCR.Web;

/// <summary>All /api routes — exact request/response parity with the FastAPI routers.</summary>
public static class ApiEndpoints
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // ── Health / about ─────────────────────────────────────────────
        api.MapGet("/health", IResult (IOptions<FcrOptions> o) => Results.Ok(new
        {
            status = "ok",
            version = FcrConstants.AppVersion,
            db = o.Value.DbPath,
            data_dir = o.Value.DataDir,
            dotnet = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
        })).AllowAnonymous();

        api.MapGet("/about", async Task<IResult> (IFcrRepository repo, IOptions<FcrOptions> o) =>
            Results.Ok(new
            {
                app_name = FcrConstants.AppName,
                version = FcrConstants.AppVersion,
                author = "Farzad Meshkipour",
                company = "SINOPEC International — YADAVARAN",
                dotnet = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                frozen = false,
                data_dir = o.Value.DataDir,
                db_path = o.Value.DbPath,
                schema_version = FcrConstants.SchemaVersion,
                records = (await repo.ListAsync(new FcrQuery())).Count,
            }));

        // ── FCR CRUD ────────────────────────────────────────────────────
        api.MapGet("/fcr", async Task<IResult> (IFcrRepository repo, IFolderService folders,
            string? search, string? pedec_status, string? fcr_status, string? exec_status,
            string? category, string? status_group, string? sort_by, string? sort_order) =>
        {
            try
            {
                var rows = await repo.ListAsync(new FcrQuery(search, pedec_status, fcr_status,
                    exec_status, category, status_group, sort_by ?? "date", sort_order ?? "desc"));
                return Results.Ok(rows.Select(r => FcrJson.ToDict(r, folders.GenerateFolderPath(r.FcrCode))).ToList());
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        api.MapGet("/fcr/{id:long}", async Task<IResult> (long id, IFcrRepository repo, IFolderService folders) =>
        {
            var rec = await repo.GetAsync(id);
            return rec is null ? Detail(404, "FCR not found")
                : Results.Ok(FcrJson.ToDict(rec, folders.GenerateFolderPath(rec.FcrCode)));
        });

        api.MapPost("/fcr", async Task<IResult> (FcrRecordInput input, IFcrRepository repo, IFolderService folders) =>
        {
            try
            {
                var rec = await repo.CreateAsync(input);
                return Results.Json(FcrJson.ToDict(rec, folders.GenerateFolderPath(rec.FcrCode)),
                    statusCode: 201);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
            catch (FcrDuplicateCodeException ex) { return Detail(409, ex.Message); }
        });

        api.MapPut("/fcr/{id:long}", async Task<IResult> (long id, FcrRecordInput input,
            IFcrRepository repo, IFolderService folders) =>
        {
            try
            {
                var rec = await repo.UpdateAsync(id, input);
                return Results.Ok(FcrJson.ToDict(rec, folders.GenerateFolderPath(rec.FcrCode)));
            }
            catch (FcrNotFoundException) { return Detail(404, "FCR not found"); }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
            catch (FcrDuplicateCodeException ex) { return Detail(409, ex.Message); }
        });

        api.MapDelete("/fcr/{id:long}", async Task<IResult> (long id, IFcrRepository repo) =>
            await repo.DeleteAsync(id)
                ? Results.Ok(new Dictionary<string, object?> { ["deleted"] = id })
                : Detail(404, "FCR not found"));

        api.MapPost("/fcr/bulk-save", async Task<IResult> (BulkSaveRequest req, IFcrRepository repo) =>
        {
            var result = await repo.BulkSaveAsync(req.Records);
            return Results.Ok(new Dictionary<string, object?>
            {
                ["saved"] = result.Saved, ["skipped"] = result.Skipped, ["errors"] = result.Errors,
            });
        });

        api.MapPost("/fcr/bulk-delete", async Task<IResult> (BulkDeleteRequest req, IFcrRepository repo) =>
            Results.Ok(new Dictionary<string, object?> { ["deleted"] = await repo.BulkDeleteAsync(req.Ids) }));

        // ── Stats / options / notifications ────────────────────────────
        api.MapGet("/stats", IResult (IStatsService stats) => Results.Ok(stats.GetStats()));
        api.MapGet("/options", IResult (IStatsService stats) => Results.Ok(stats.GetOptions()));
        api.MapGet("/notifications", IResult (IStatsService stats) => Results.Ok(stats.GetNotifications()));

        // ── Export ──────────────────────────────────────────────────────
        api.MapGet("/export", IResult (IExcelExportService excel) =>
        {
            var bytes = excel.BuildExport(withCharts: false);
            return bytes is null ? Detail(404, "No records to export")
                : Results.File(bytes, XlsxType, $"FCR_Export_{DateTime.Now:yyyy-MM-dd}.xlsx");
        });

        api.MapGet("/export-with-charts", IResult (IExcelExportService excel) =>
        {
            var bytes = excel.BuildExport(withCharts: true);
            return bytes is null ? Detail(404, "No records to export")
                : Results.File(bytes, XlsxType, $"FCR_Report_{DateTime.Now:yyyy-MM-dd}.xlsx");
        });

        api.MapPost("/auto-export/run", IResult (IExcelExportService excel, ISettingsStore settings) =>
        {
            var s = settings.Load();
            var dir = settings.ResolveUserPath(s.AutoExportLocation, "exports");
            var dest = Path.Combine(dir, $"FCR_AutoExport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            var result = excel.ExportToFile(dest);
            if (result.TryGetValue("ok", out var ok) && ok is true)
            {
                s.LastAutoExport = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                settings.Save(s);
            }
            return Results.Ok(result);
        });

        // ── Settings ────────────────────────────────────────────────────
        api.MapGet("/settings", IResult (ISettingsStore settings) => Results.Ok(settings.Payload()));

        api.MapPut("/settings", IResult (JsonObject body, ISettingsStore settings) =>
        {
            var current = settings.Load();
            var merged = System.Text.Json.JsonSerializer.SerializeToNode(current)!.AsObject();
            var known = new HashSet<string>(merged.Select(kv => kv.Key));
            foreach (var kv in body)
            {
                if (!known.Contains(kv.Key) || kv.Value is null) continue;
                merged[kv.Key] = kv.Value.DeepClone();
            }
            foreach (var key in new[] { "auto_export_interval_days", "auto_backup_interval_days", "pedec_alert_days" })
            {
                if (merged[key] is not null)
                {
                    var v = 1;
                    try { v = Math.Max(1, merged[key]!.GetValue<int>()); }
                    catch { v = 1; }
                    merged[key] = v;
                }
            }
            if (merged["language"] is not null
                && merged["language"]!.GetValue<string>() is not ("en" or "zh"))
                merged["language"] = "en";
            settings.Save(merged.Deserialize<FcrSettings>()!);
            return Results.Ok(settings.Payload());
        });

        api.MapPost("/settings/reset", IResult (ISettingsStore settings) =>
        {
            try
            {
                settings.Reset();
                return Results.Ok(new Dictionary<string, object?>
                {
                    ["ok"] = true, ["message"] = "Settings reset to defaults",
                });
            }
            catch (Exception exc) { return Detail(500, $"Reset failed: {exc.Message}"); }
        });

        // ── Backup ──────────────────────────────────────────────────────
        api.MapPost("/backup", async Task<IResult> (FcrBackupService backups) =>
        {
            try { return Results.Ok(await backups.CreateBackupAsync()); }
            catch (Exception exc) { return Detail(500, $"Backup failed: {exc.Message}"); }
        });

        api.MapGet("/backups", IResult (FcrBackupService backups) => Results.Ok(backups.ListBackups()));

        // ── Folders ─────────────────────────────────────────────────────
        api.MapGet("/fcr/{id:long}/open-folder", async Task<IResult> (long id, IFcrRepository repo, IFolderService folders) =>
        {
            var rec = await repo.GetAsync(id);
            if (rec is null) return Detail(404, "FCR not found");
            var code = (rec.FcrCode ?? "").Trim();
            if (code.Length == 0)
                return Results.Ok(new { ok = false, error = "FCR Code is empty" });
            var path = folders.ResolveFolderPath(code);
            if (path.Length == 0)
                return Results.Ok(new { ok = false, error = $"Cannot derive a folder path from FCR Code '{code}'" });
            if (!Directory.Exists(path))
                return Results.Ok(new { ok = false, error = $"Folder not found:\n{path}" });
            // A centralized server cannot open folders on the caller's machine;
            // report the resolved path — the client may map it locally.
            return Results.Ok(new { ok = true, path });
        });

        api.MapPost("/fcr/{id:long}/create-folder", async Task<IResult> (long id, IFcrRepository repo, IFolderService folders) =>
        {
            var rec = await repo.GetAsync(id);
            if (rec is null) return Detail(404, "FCR not found");
            var code = (rec.FcrCode ?? "").Trim();
            if (code.Length == 0)
                return Results.Ok(new { ok = false, error = "FCR Code is empty" });
            var path = folders.ResolveFolderPath(code);
            if (path.Length == 0)
                return Results.Ok(new { ok = false, error = "Cannot generate folder path from FCR Code" });
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception exc)
            {
                return Results.Ok(new { ok = false, error = $"Cannot create folder:\n{path}\n{exc.Message}" });
            }
            return Results.Ok(new { ok = true, path });
        });
    }

    private static IResult Detail(int status, string message) =>
        Results.Json(new Dictionary<string, object?> { ["detail"] = message }, statusCode: status);
}
