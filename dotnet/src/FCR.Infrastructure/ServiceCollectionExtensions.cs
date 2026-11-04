using FCR.Application;
using FCR.Infrastructure.Backup;
using FCR.Infrastructure.Archive;
using FCR.Infrastructure.Excel;
using FCR.Infrastructure.Folders;
using FCR.Infrastructure.Persistence;
using FCR.Infrastructure.Scheduling;
using FCR.Infrastructure.Settings;
using FCR.Infrastructure.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCR.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers FcrOptions, FcrDbContext, migrator, repository and all feature services.</summary>
    public static IServiceCollection AddFcrInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<FcrOptions>(o =>
        {
            o.DataDir = config["FCR_DATA_DIR"]
                ?? config["Fcr:DataDir"]
                ?? Path.Combine(AppContext.BaseDirectory, "data");
            o.BackupLocation = config["Fcr:BackupLocation"] ?? "backups";
            o.AttachmentRoot = config["Fcr:AttachmentRoot"];
            o.RequestAttachmentDir = config["Fcr:RequestAttachmentDir"] ?? o.RequestAttachmentDir;
            o.IdentityExternalIdClaim = config["Fcr:Identity:ExternalIdClaim"]
                ?? o.IdentityExternalIdClaim;
            o.AllowLocalIdFallback = bool.TryParse(config["Fcr:Identity:AllowLocalIdFallback"], out var allowLocalFallback)
                && allowLocalFallback;
            o.BootstrapAdminExternalId = config["Fcr:BootstrapAdmin:ExternalId"];
            o.BootstrapAdminUsername = config["Fcr:BootstrapAdmin:Username"];
            o.BootstrapAdminDisplayName = config["Fcr:BootstrapAdmin:DisplayName"];
            o.BootstrapAdminEmail = config["Fcr:BootstrapAdmin:Email"];
        });
        services.AddDbContext<FcrDbContext>((sp, b) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FcrOptions>>().Value;
            Directory.CreateDirectory(opts.DataDir);
            b.UseSqlite(opts.ConnectionString);
        });
        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<IFcrRepository, FcrRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddSingleton<Attachments.AttachmentStore>();
        services.AddScoped<FcrArchiveService>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<FcrBackupService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddHostedService<AutoOpsService>();
        return services;
    }
}
