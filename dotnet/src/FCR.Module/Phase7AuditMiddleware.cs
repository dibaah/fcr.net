using System.Security.Claims;
using FCR.Domain.Entities;
using FCR.Infrastructure.Persistence;

namespace FCR.Web;

/// <summary>
/// Records mutating API request metadata for audit. Does not persist bodies or
/// query strings, so credentials and sensitive form fields are not captured.
/// </summary>
public sealed class Phase7AuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (!context.Request.Path.StartsWithSegments("/api")
            || HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method))
            return;

        try
        {
            var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            long? userId = long.TryParse(userIdText, out var parsed) ? parsed : null;
            var username = context.User.Identity?.Name;
            var path = context.Request.Path.Value ?? "/";
            var requestId = ExtractRequestId(path);
            var action = $"{context.Request.Method} {path}";

            var db = context.RequestServices.GetRequiredService<FcrDbContext>();
            db.FcrAuditLogs.Add(new FcrAuditLog
            {
                UserId = userId,
                Username = username,
                Action = action,
                HttpMethod = context.Request.Method,
                Path = path,
                StatusCode = context.Response.StatusCode,
                RequestId = requestId,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // Audit failure must not turn a completed business request into a 500.
            context.RequestServices.GetRequiredService<ILogger<Phase7AuditMiddleware>>()
                .LogError(ex, "Failed to persist Phase 7 audit event for {Path}", context.Request.Path);
        }
    }

    private static string? ExtractRequestId(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var requestsIndex = Array.FindIndex(parts, p => p.Equals("requests", StringComparison.OrdinalIgnoreCase));
        if (requestsIndex >= 0 && requestsIndex + 1 < parts.Length)
        {
            var candidate = parts[requestsIndex + 1];
            if (candidate.Length >= 16 && candidate.Contains('-')) return candidate;
        }
        return null;
    }
}
