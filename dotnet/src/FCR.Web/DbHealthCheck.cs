using FCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FCR.Web;

/// <summary>Readiness probe: verifies the SQLite database answers queries.</summary>
public sealed class DbHealthCheck(FcrDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("database reachable");
        }
        catch (Exception exc)
        {
            return HealthCheckResult.Unhealthy("database unreachable", exc);
        }
    }
}
