using FCR.Domain;
using FCR.Infrastructure;
using FCR.Infrastructure.Persistence;
using FCR.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCR.Module;

/// <summary>
/// Registration and endpoint integration for hosting FCR inside an existing
/// ASP.NET Core application. Authentication remains the host application's job.
/// </summary>
public static class FcrModuleExtensions
{
    /// <summary>
    /// Registers FCR persistence, domain services, background operations,
    /// health checks, and FCR authorization policies. Does not add an auth
    /// scheme, local login, local admin seeding, or run migrations.
    /// </summary>
    public static IServiceCollection AddFcrModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFcrInfrastructure(configuration);
        services.AddHealthChecks().AddCheck<FCR.Web.DbHealthCheck>("fcr_db");

        services.AddAuthorization(options =>
        {
            foreach (var role in RoleNames.All)
                options.AddPolicy(role, policy => policy.RequireRole(role));

            options.AddPolicy(PolicyNames.FcrModuleAccess,
                policy => policy.RequireRole(RoleNames.FcrUser, RoleNames.FcrAdmin,
                    RoleNames.Requester, RoleNames.Reviewer, RoleNames.Approver,
                    RoleNames.SystemAdministrator));

            options.AddPolicy(PolicyNames.AdminConsole,
                policy => policy.RequireRole(RoleNames.FcrAdmin, RoleNames.SystemAdministrator));
            options.AddPolicy(PolicyNames.RequesterAccess,
                policy => policy.RequireRole(RoleNames.Requester, RoleNames.SystemAdministrator));
            options.AddPolicy(PolicyNames.ReviewerAccess,
                policy => policy.RequireRole(RoleNames.Reviewer, RoleNames.Approver,
                    RoleNames.SystemAdministrator));
        });

        return services;
    }

    /// <summary>Adds FCR audit middleware. Call after UseAuthentication().</summary>
    public static IApplicationBuilder UseFcrModule(this IApplicationBuilder app)
    {
        app.UseMiddleware<FcrIdentityMiddleware>();
        app.UseMiddleware<FCR.Web.Phase7AuditMiddleware>();
        return app;
    }

    /// <summary>
    /// Maps FCR feature endpoints into the host's route table. The host must
    /// configure authentication and call UseAuthorization() before endpoint execution.
    /// FCR's local development login endpoints are intentionally not mapped here.
    /// </summary>
    public static WebApplication MapFcrModule(this WebApplication app)
    {
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");
        app.MapApi();
        app.MapRequestApi();
        app.MapPhase7Reports();
        app.MapFcrIdentityUsers();
        return app;
    }

    /// <summary>Explicitly applies FCR database migrations. Call during controlled host startup.</summary>
    public static async Task InitializeFcrModuleAsync(
        this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAsync(cancellationToken);
        await UserSeeder.SeedRolesAsync(scope.ServiceProvider.GetRequiredService<FcrDbContext>());
    }
}
