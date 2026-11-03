using System.Security.Claims;
using FCR.Application;
using FCR.Infrastructure;
using Microsoft.Extensions.Options;

namespace FCR.Module;

/// <summary>
/// Resolves the authenticated ERMS subject to an FCR-local user row and refreshes
/// FCR identity/role claims from the database on every request. Unmapped ERMS users
/// stay authenticated to the host but receive no FCR roles or internal user ID.
/// </summary>
public sealed class FcrIdentityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context, IUserService users, IOptions<FcrOptions> options)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var subjectClaim = options.Value.IdentityExternalIdClaim;
        var externalId = principal.FindFirst(subjectClaim)?.Value;
        UserDto? user = null;

        if (!string.IsNullOrWhiteSpace(externalId))
            user = await users.GetUserByExternalIdAsync(externalId);

        // Compatibility for standalone development cookies and hosts that
        // explicitly pass an existing FCR internal numeric ID.
        if (user is null && long.TryParse(
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var localId))
            user = await users.GetUserAsync(localId);

        context.User = user is { IsActive: true }
            ? BuildFcrPrincipal(principal, user)
            : RemoveFcrAuthorizationClaims(principal);

        await next(context);
    }

    private static ClaimsPrincipal BuildFcrPrincipal(ClaimsPrincipal source, UserDto user)
    {
        var sourceIdentity = source.Identity as ClaimsIdentity;
        var claims = source.Claims.Where(c =>
            c.Type != ClaimTypes.NameIdentifier
            && c.Type != ClaimTypes.Name
            && c.Type != ClaimTypes.Role);
        var identity = new ClaimsIdentity(
            claims, sourceIdentity?.AuthenticationType ?? "FCR",
            ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));
        foreach (var role in user.Roles.Distinct(StringComparer.Ordinal))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal RemoveFcrAuthorizationClaims(ClaimsPrincipal source)
    {
        var sourceIdentity = source.Identity as ClaimsIdentity;
        var claims = source.Claims.Where(c =>
            c.Type != ClaimTypes.NameIdentifier
            && c.Type != ClaimTypes.Name
            && c.Type != ClaimTypes.Role);
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims, sourceIdentity?.AuthenticationType ?? "FCR",
            ClaimTypes.Name, ClaimTypes.Role));
    }
}
