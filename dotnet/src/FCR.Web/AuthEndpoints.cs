using System.Security.Claims;
using FCR.Application;
using FCR.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FCR.Web;

/// <summary>
/// Development authentication + user/role administration.
/// Cookie sessions over a local user table — explicitly a dev mechanism for
/// controlled testing (master plan: production auth provider is an IT decision).
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthApi(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").RequireAuthorization();

        auth.MapPost("/login", async Task<IResult> (
            LoginRequest req, IUserService users, HttpContext http) =>
        {
            var user = await users.VerifyCredentialsAsync(req.Username ?? "", req.Password ?? "");
            if (user is null)
                return Detail(401, "Invalid username or password");
            await SignInAsync(http, user);
            return Results.Ok(user);
        }).AllowAnonymous();

        auth.MapPost("/logout", async Task<IResult> (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { ok = true });
        });

        auth.MapGet("/me", IResult (HttpContext http) =>
        {
            var p = http.User;
            return Results.Ok(new
            {
                id = p.FindFirstValue(ClaimTypes.NameIdentifier),
                username = p.Identity?.Name ?? "",
                roles = p.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            });
        });

        // ── User & role administration (System Administrator only) ────────
        var admin = app.MapGroup("/api/admin").RequireAuthorization(RoleNames.SystemAdministrator);

        admin.MapGet("/roles", async Task<IResult> (IUserService users) =>
            Results.Ok(await users.ListRolesAsync()));

        admin.MapGet("/users", async Task<IResult> (IUserService users) =>
            Results.Ok(await users.ListUsersAsync()));

        admin.MapPost("/users", async Task<IResult> (CreateUserRequest req, IUserService users) =>
        {
            try { return Results.Json(await users.CreateUserAsync(req), statusCode: 201); }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        admin.MapPut("/users/{id:long}", async Task<IResult> (
            long id, UpdateUserRequest req, IUserService users) =>
        {
            try
            {
                var user = await users.UpdateUserAsync(id, req);
                return user is null ? Detail(404, "User not found") : Results.Ok(user);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });
    }

    internal static async Task SignInAsync(HttpContext http, UserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private static IResult Detail(int status, string message) =>
        Results.Json(new Dictionary<string, object?> { ["detail"] = message }, statusCode: status);
}
