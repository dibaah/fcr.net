using FCR.Application;
using FCR.Domain;
using FCR.Infrastructure.Backup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FCR.Infrastructure.Scheduling;

/// <summary>
/// Auto backup + auto export — parity with app/services/scheduler.py:
/// a 60-second background loop that runs each task when its interval has elapsed.
/// </summary>
public sealed class AutoOpsService(
    ISettingsStore settings,
    IServiceScopeFactory scopeFactory,
    IOptions<FcrOptions> options,
    ILogger<AutoOpsService> log) : BackgroundService
{
    private readonly FcrOptions _o = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (TaskCanceledException) { break; }
            try { MaybeAutoExport(); }
            catch (Exception exc) { log.LogWarning("Auto export check failed: {Error}", exc.Message); }
            try { MaybeAutoBackup(); }
            catch (Exception exc) { log.LogWarning("Auto backup check failed: {Error}", exc.Message); }
        }
    }

    private static bool Due(string lastValue, int intervalDays)
    {
        var last = FcrDate.Parse(lastValue);
        return last is null || DateTime.Now - last.Value >= TimeSpan.FromDays(intervalDays);
    }

    private void MaybeAutoExport()
    {
        var s = settings.Load();
        if (!s.AutoExportEnabled) return;
        if (!Due(s.LastAutoExport, Math.Max(1, s.AutoExportIntervalDays))) return;
        var dir = settings.ResolveUserPath(s.AutoExportLocation, "exports");
        var dest = Path.Combine(dir, $"FCR_AutoExport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        using var scope = scopeFactory.CreateScope();
        var excel = scope.ServiceProvider.GetRequiredService<IExcelExportService>();
        var result = excel.ExportToFile(dest);
        if (result.TryGetValue("ok", out var ok) && ok is true)
        {
            s.LastAutoExport = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            settings.Save(s);
            log.LogInformation("Auto Excel export: {Path}", dest);
        }
    }

    private void MaybeAutoBackup()
    {
        var s = settings.Load();
        if (!s.AutoBackupEnabled) return;
        if (!Due(s.LastAutoBackup, Math.Max(1, s.AutoBackupIntervalDays))) return;
        using var scope = scopeFactory.CreateScope();
        var backups = scope.ServiceProvider.GetRequiredService<FcrBackupService>();
        backups.CreateBackupAsync().GetAwaiter().GetResult();
        s.LastAutoBackup = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        settings.Save(s);
        log.LogInformation("Auto backup complete");
    }
}
