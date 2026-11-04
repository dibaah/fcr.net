using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FCR.Tests;

/// <summary>
/// Production standalone host must not expose development login/user-admin endpoints
/// or seed the default local admin account.
/// </summary>
public sealed class ProductionAuthTests : IClassFixture<ProductionAuthTests.Fixture>
{
    public sealed class Fixture : WebApplicationFactory<Program>
    {
        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"fcr_prod_auth_{Guid.NewGuid():N}");

        public Fixture()
        {
            Directory.CreateDirectory(_dataDir);
            var source = TestDb.RepoDbPath;
            if (File.Exists(source))
                File.Copy(source, Path.Combine(_dataDir, "fcr_database.db"));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FCR_DATA_DIR"] = _dataDir,
                    ["Fcr:AttachmentRoot"] = _dataDir,
                }));
        }
    }

    private readonly HttpClient _client;

    public ProductionAuthTests(Fixture fixture) => _client = fixture.CreateClient();

    [Fact]
    public async Task Production_DoesNotExposeDevelopmentLoginOrUserAdmin()
    {
        var login = await _client.PostAsync("/api/auth/login", content: null);
        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);

        var users = await _client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.NotFound, users.StatusCode);

        // FCR APIs remain protected; disabling local login must not make them anonymous.
        var fcr = await _client.GetAsync("/api/fcr");
        Assert.Equal(HttpStatusCode.Unauthorized, fcr.StatusCode);
    }
}
