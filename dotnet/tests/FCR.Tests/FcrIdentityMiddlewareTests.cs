using System.Security.Claims;
using FCR.Application;
using FCR.Infrastructure;
using FCR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FCR.Tests;

public class FcrIdentityMiddlewareTests
{
    [Fact]
    public async Task MappedErmsSubject_ReceivesFcrInternalIdAndAssignedRoles()
    {
        var (dir, options) = TestDb.CopyRepoDb();
        await using var db = TestDb.Context(options);
        await new DatabaseMigrator(db).MigrateAsync();
        await UserSeeder.SeedAsync(db, "admin");
        var users = new UserService(db);
        var fcrUser = await users.CreateExternalUserAsync(new CreateExternalUserRequest(
            "erms-subject-middleware", "erms.middleware", "ERMS Middleware", "m@example.invalid",
            ["FCR User", "Requester"]));

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "erms-subject-middleware"),
                new Claim(ClaimTypes.Name, "upstream-name"),
                new Claim(ClaimTypes.Role, "ERMS Employee"),
            ], "ERMS-test"));

        var middleware = new FCR.Module.FcrIdentityMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, users,
            Options.Create(new FcrOptions { DataDir = dir }));

        Assert.True(context.User.Identity?.IsAuthenticated);
        Assert.Equal(fcrUser.Id.ToString(), context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("erms.middleware", context.User.Identity?.Name);
        Assert.True(context.User.IsInRole("FCR User"));
        Assert.True(context.User.IsInRole("Requester"));
        Assert.False(context.User.IsInRole("ERMS Employee"));
    }

    [Fact]
    public async Task UnmappedErmsSubject_DoesNotKeepUntrustedFcrRoleClaims()
    {
        var (dir, options) = TestDb.CopyRepoDb();
        await using var db = TestDb.Context(options);
        await new DatabaseMigrator(db).MigrateAsync();
        await UserSeeder.SeedAsync(db, "admin");
        var users = new UserService(db);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "unknown-erms-subject"),
                new Claim(ClaimTypes.Role, "System Administrator"),
                new Claim(ClaimTypes.Role, "FCR Admin"),
            ], "ERMS-test"));

        var middleware = new FCR.Module.FcrIdentityMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, users,
            Options.Create(new FcrOptions { DataDir = dir }));

        Assert.True(context.User.Identity?.IsAuthenticated);
        Assert.Null(context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.False(context.User.IsInRole("System Administrator"));
        Assert.False(context.User.IsInRole("FCR Admin"));
    }
    [Fact]
    public async Task UnmappedNumericErmsSubject_CannotFallBackToLocalAdminId()
    {
        var (dir, options) = TestDb.CopyRepoDb();
        await using var db = TestDb.Context(options);
        await new DatabaseMigrator(db).MigrateAsync();
        await UserSeeder.SeedAsync(db, "admin");
        var users = new UserService(db);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, "ERMS Employee"),
            ], "ERMS-test"));

        var middleware = new FCR.Module.FcrIdentityMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, users,
            Options.Create(new FcrOptions { DataDir = dir }));

        Assert.True(context.User.Identity?.IsAuthenticated);
        Assert.Null(context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.False(context.User.IsInRole("System Administrator"));
    }

}
