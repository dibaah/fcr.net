using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FCR.Tests;

/// <summary>
/// Phase 4 authorization tests: unauthenticated access, login/logout sessions,
/// role-based admin access control and privilege-escalation scenarios.
/// </summary>
public class AuthTests(ApiParityTests.Fixture fixture)
    : IClassFixture<ApiParityTests.Fixture>
{
    private static async Task<JsonElement> Body(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<HttpClient?> Login(string username, string password)
    {
        var c = fixture.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login",
            new Dictionary<string, object?> { ["username"] = username, ["password"] = password });
        return r.IsSuccessStatusCode ? c : null;
    }

    [Fact]
    public async Task Unauthenticated_Requests_Return401()
    {
        var anon = fixture.CreateClient();
        foreach (var route in new[]
                 { "/api/fcr", "/api/stats", "/api/settings", "/api/admin/users", "/api/auth/me" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(route)).StatusCode);
        // login page assets + anonymous endpoints stay reachable
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/static/script.js")).StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        var c = fixture.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login",
            new Dictionary<string, object?> { ["username"] = "admin", ["password"] = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("Invalid username or password",
            (await Body(r)).GetProperty("detail").GetString());
        Assert.Null(await Login("admin", "wrong"));
    }

    [Fact]
    public async Task Login_Me_Logout_SessionLifecycle()
    {
        var c = await Login("admin", "admin");
        Assert.NotNull(c);
        var me = await Body(await c.GetAsync("/api/auth/me"));
        Assert.Equal("admin", me.GetProperty("username").GetString());
        Assert.Contains("System Administrator",
            me.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));

        var out_ = await c.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, out_.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await c.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Admin_RolesAndUserManagement()
    {
        var admin = await Login("admin", "admin");
        Assert.NotNull(admin);

        var roles = (await Body(await admin.GetAsync("/api/admin/roles"))).EnumerateArray()
            .Select(r => r.GetProperty("name").GetString()).ToList();
        Assert.Equal(6, roles.Count);
        Assert.Contains("FCR User", roles);
        Assert.Contains("Requester", roles);
        Assert.Contains("FCR Admin", roles);
        Assert.Contains("Reviewer", roles);
        Assert.Contains("Approver", roles);
        Assert.Contains("System Administrator", roles);

        // multi-role user (a user may have more than one role if authorized)
        var create = await admin.PostAsJsonAsync("/api/admin/users",
            new Dictionary<string, object?>
            {
                ["username"] = "reviewer1", ["display_name"] = "Review One",
                ["password"] = "pass1234", ["roles"] = new[] { "Requester", "Reviewer" },
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var user = await Body(create);
        Assert.Equal(2, user.GetProperty("roles").GetArrayLength());

        var users = (await Body(await admin.GetAsync("/api/admin/users"))).EnumerateArray().ToList();
        Assert.Contains(users, u => u.GetProperty("username").GetString() == "reviewer1");

        var dup = await admin.PostAsJsonAsync("/api/admin/users",
            new Dictionary<string, object?> { ["username"] = "reviewer1", ["password"] = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);

        var badRole = await admin.PostAsJsonAsync("/api/admin/users",
            new Dictionary<string, object?>
            { ["username"] = "nobody2", ["password"] = "x", ["roles"] = new[] { "Superuser" } });
        Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);
    }

    [Fact]
    public async Task FcrAdmin_CanMapErmsIdentityAndAssignFcrRoles()
    {
        var admin = await Login("admin", "admin");
        Assert.NotNull(admin);

        var create = await admin.PostAsJsonAsync("/api/admin/fcr-users",
            new Dictionary<string, object?>
            {
                ["external_id"] = "erms-subject-001",
                ["username"] = "erms.user1",
                ["display_name"] = "ERMS User One",
                ["email"] = "erms.user1@example.invalid",
                ["roles"] = new[] { "FCR User", "Requester" },
                ["is_active"] = true,
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var user = await Body(create);
        var id = user.GetProperty("id").GetInt64();
        Assert.Equal("erms-subject-001", user.GetProperty("external_id").GetString());
        Assert.Contains("FCR User", user.GetProperty("roles").EnumerateArray()
            .Select(x => x.GetString()));

        var update = await admin.PutAsJsonAsync($"/api/admin/fcr-users/{id}",
            new Dictionary<string, object?> { ["roles"] = new[] { "FCR User", "FCR Admin" } });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await Body(update);
        var roles = updated.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("FCR Admin", roles);
        Assert.DoesNotContain("Requester", roles);
    }

    [Fact]
    public async Task NonAdmin_ForbiddenFrom_AdminEndpoints()
    {
        var admin = await Login("admin", "admin");
        Assert.NotNull(admin);
        await admin.PostAsJsonAsync("/api/admin/users", new Dictionary<string, object?>
        {
            ["username"] = "plainuser", ["password"] = "pass1234",
            ["roles"] = new[] { "Requester" },
        });

        var c = await Login("plainuser", "pass1234");
        Assert.NotNull(c);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/fcr")).StatusCode);        // authenticated OK
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/roles")).StatusCode);
        var escalation = await c.PostAsJsonAsync("/api/admin/users", new Dictionary<string, object?>
        {
            ["username"] = "selfadmin", ["password"] = "x", ["roles"] = new[] { "System Administrator" },
        });
        Assert.Equal(HttpStatusCode.Forbidden, escalation.StatusCode);
    }

    [Fact]
    public async Task FcrUserCanAccessModule_FcrAdminCanManageMappings_EmptyRoleUserDenied()
    {
        var admin = await Login("admin", "admin");
        Assert.NotNull(admin);

        await admin.PostAsJsonAsync("/api/admin/users", new Dictionary<string, object?>
        {
            ["username"] = "fcruser", ["password"] = "pass1234",
            ["roles"] = new[] { "FCR User" },
        });
        await admin.PostAsJsonAsync("/api/admin/users", new Dictionary<string, object?>
        {
            ["username"] = "fcradmin", ["password"] = "pass1234",
            ["roles"] = new[] { "FCR Admin" },
        });
        await admin.PostAsJsonAsync("/api/admin/users", new Dictionary<string, object?>
        {
            ["username"] = "norole", ["password"] = "pass1234",
        });

        var fcrUser = await Login("fcruser", "pass1234");
        Assert.NotNull(fcrUser);
        Assert.Equal(HttpStatusCode.OK, (await fcrUser.GetAsync("/api/fcr")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fcrUser.GetAsync("/api/admin/fcr-users")).StatusCode);

        var fcrAdmin = await Login("fcradmin", "pass1234");
        Assert.NotNull(fcrAdmin);
        Assert.Equal(HttpStatusCode.OK,
            (await fcrAdmin.GetAsync("/api/admin/fcr-users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fcrAdmin.GetAsync("/api/admin/users")).StatusCode);

        var noRole = await Login("norole", "pass1234");
        Assert.NotNull(noRole);
        Assert.Equal(HttpStatusCode.Forbidden, (await noRole.GetAsync("/api/fcr")).StatusCode);
    }

    [Fact]
    public async Task RoleChange_TakesEffectImmediately()
    {
        var admin = await Login("admin", "admin");
        Assert.NotNull(admin);
        var create = await admin.PostAsJsonAsync("/api/admin/users", new Dictionary<string, object?>
        {
            ["username"] = "promoted", ["password"] = "pass1234", ["roles"] = new[] { "Requester" },
        });
        var id = (await Body(create)).GetProperty("id").GetInt64();

        var c = await Login("promoted", "pass1234");
        Assert.NotNull(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode);

        var promote = await admin.PutAsJsonAsync($"/api/admin/users/{id}",
            new Dictionary<string, object?> { ["roles"] = new[] { "System Administrator" } });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_SessionRejected()
    {
        var admin = await Login("admin", "admin");
        Assert.NotNull(admin);
        var create = await admin.PostAsJsonAsync("/api/admin/users", new Dictionary<string, object?>
        {
            ["username"] = "exuser", ["password"] = "pass1234", ["roles"] = new[] { "Requester" },
        });
        var id = (await Body(create)).GetProperty("id").GetInt64();

        var c = await Login("exuser", "pass1234");
        Assert.NotNull(c);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/fcr")).StatusCode);

        await admin.PutAsJsonAsync($"/api/admin/users/{id}",
            new Dictionary<string, object?> { ["is_active"] = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/fcr")).StatusCode);
        Assert.Null(await Login("exuser", "pass1234"));
    }
}
