using FCR.Application;
using FCR.Domain;
using FCR.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCR.Tests;

public class FcrModuleBootstrapTests
{
    [Fact]
    public async Task InitializeModule_BootstrapsFirstErmsAdminOnlyOnce()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "fcr-module-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Fcr:DataDir"] = dataDir,
                    ["Fcr:AttachmentRoot"] = Path.Combine(dataDir, "archive"),
                    ["Fcr:BootstrapAdmin:ExternalId"] = "erms-bootstrap-subject",
                    ["Fcr:BootstrapAdmin:Username"] = "erms-bootstrap",
                    ["Fcr:BootstrapAdmin:DisplayName"] = "Initial FCR Admin",
                    ["Fcr:BootstrapAdmin:Email"] = "bootstrap@example.invalid",
                }).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFcrModule(configuration);
            await using var provider = services.BuildServiceProvider();

            await provider.InitializeFcrModuleAsync();
            using (var scope = provider.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<IUserService>();
                var list = await users.ListUsersAsync();
                var admin = Assert.Single(list);
                Assert.Equal("erms-bootstrap", admin.Username);
                Assert.Equal("erms-bootstrap-subject", admin.ExternalId);
                Assert.Contains(RoleNames.FcrAdmin, admin.Roles);
                Assert.Contains(RoleNames.FcrUser, admin.Roles);
                Assert.DoesNotContain(list, u => u.Username == "admin");
            }

            await provider.InitializeFcrModuleAsync();
            using (var scope = provider.CreateScope())
                Assert.Single(await scope.ServiceProvider.GetRequiredService<IUserService>().ListUsersAsync());
        }
        finally
        {
            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeModuleWithoutBootstrapConfig_DoesNotCreateLocalAdmin()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "fcr-module-no-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Fcr:DataDir"] = dataDir }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFcrModule(configuration);
            await using var provider = services.BuildServiceProvider();

            await provider.InitializeFcrModuleAsync();
            using var scope = provider.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserService>();
            Assert.Empty(await users.ListUsersAsync());
        }
        finally
        {
            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
        }
    }
}
