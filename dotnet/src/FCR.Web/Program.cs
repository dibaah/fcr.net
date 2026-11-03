using System.Security.Claims;
using System.Text.Json;
using FCR.Application;
using FCR.Domain;
using FCR.Infrastructure;
using FCR.Module;
using FCR.Infrastructure.Persistence;
using FCR.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// HTTPS is opt-in: set Fcr:HttpsCertPath (+ optional HttpsCertPassword/HttpsPort)
// and Kestrel also listens on the HTTPS endpoint. HTTP endpoints come from the
// "Kestrel" config section (appsettings or env vars).
builder.WebHost.ConfigureKestrel(kestrel =>
{
    var certPath = builder.Configuration["Fcr:HttpsCertPath"];
    if (!string.IsNullOrWhiteSpace(certPath))
        kestrel.ListenAnyIP(builder.Configuration.GetValue("Fcr:HttpsPort", 8443),
            listen => listen.UseHttps(certPath, builder.Configuration["Fcr:HttpsCertPassword"]));
});

builder.Services.AddHttpLogging(logging => logging.LoggingFields =
    Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPropertiesAndHeaders
    | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode);

// snake_case payloads so the existing frontend gets the same JSON shape FastAPI emitted.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Development authentication: cookie sessions over a local user table. Dev-only
// mechanism for controlled testing — the production provider (AD/Entra) is an
// open IT decision in the master plan; swap this registration when decided.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "fcr_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        // Session management: re-validate the user on every request so account
        // deactivation and role changes take effect immediately.
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var idStr = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var users = ctx.HttpContext.RequestServices.GetRequiredService<IUserService>();
            if (!long.TryParse(idStr, out var id) || !await users.IsActiveUserAsync(id))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }
            var roles = await users.GetRoleNamesAsync(id);
            ctx.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, idStr!),
                    new Claim(ClaimTypes.Name, ctx.Principal!.Identity!.Name ?? ""),
                }.Concat(roles.Select(r => new Claim(ClaimTypes.Role, r))),
                CookieAuthenticationDefaults.AuthenticationScheme)));
        };
    });
builder.Services.AddFcrModule(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();
    await UserSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<FcrDbContext>(),
        app.Configuration["Fcr:SeedAdminPassword"]);
}

var staticDir = ResolveStaticDir(app.Configuration, app.Environment.ContentRootPath);
if (staticDir is not null)
{
    var provider = new PhysicalFileProvider(staticDir);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider, RequestPath = "/static" });
    app.MapGet("/", () => Results.File(Path.Combine(staticDir, "index.html"), "text/html"));
    app.MapGet("/phase7-reports.html", () => Results.File(Path.Combine(staticDir, "phase7-reports.html"), "text/html"));
}

app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h.TryAdd("X-Content-Type-Options", "nosniff");
    h.TryAdd("X-Frame-Options", "SAMEORIGIN");
    h.TryAdd("Referrer-Policy", "no-referrer");
    if (context.Request.Path.StartsWithSegments("/api"))
        h.TryAdd("Cache-Control", "no-store");
    await next();
});

if (app.Configuration.GetValue("Fcr:BehindProxy", false))
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    });

app.UseHttpLogging();
app.UseAuthentication();
app.UseFcrModule();
app.UseAuthorization();

app.MapAuthApi(); // standalone development login; ERMS hosts do not map this endpoint.
app.MapFcrModule();

await app.RunAsync();

static string? ResolveStaticDir(IConfiguration config, string contentRoot)
{
    var candidates = new[]
    {
        config["FCR_STATIC_DIR"],
        config["Fcr:StaticDir"],
        Path.Combine(contentRoot, "static"),                       // deployed layout
        Path.Combine(contentRoot, "..", "..", "..", "static"),     // repo layout: dotnet/src/FCR.Web → repo/static
    };
    foreach (var c in candidates)
    {
        if (string.IsNullOrWhiteSpace(c)) continue;
        var full = Path.GetFullPath(c);
        if (Directory.Exists(full) && File.Exists(Path.Combine(full, "index.html")))
            return full;
    }
    return null;
}

public partial class Program;
