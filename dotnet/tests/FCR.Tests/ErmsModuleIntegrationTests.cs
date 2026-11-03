using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FCR.Application;
using FCR.Domain;
using FCR.Infrastructure.Persistence;
using FCR.Module;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FCR.Tests;

public sealed class ErmsModuleIntegrationTests
{
    [Fact]
    public async Task ERMSHost_UsesExternalSubjectForFcrAccess_AndDeniesUnmappedSubjects()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), $"fcr_erms_host_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDir);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FCR_DATA_DIR"] = dataDir,
            ["Fcr:Identity:ExternalIdClaim"] = "sub",
            ["Fcr:AttachmentRoot"] = Path.Combine(dataDir, "archive"),
        });
        builder.Services.AddAuthentication("ERMS-Test")
            .AddScheme<AuthenticationSchemeOptions, ErmsTestAuthHandler>("ERMS-Test", _ => { });
        builder.Services.AddFcrModule(builder.Configuration);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseFcrModule();
        app.UseAuthorization();
        app.MapFcrModule();

        try
        {
            await app.StartAsync();
            await app.Services.InitializeFcrModuleAsync();

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<IUserService>();
                await users.CreateExternalUserAsync(new CreateExternalUserRequest(
                    "erms-subject-known", "erms.integration", "ERMS Integration User",
                    "erms.integration@example.invalid", ["FCR User", "Requester"]));
            }

            using var client = app.GetTestClient();

            // ERMS-authenticated but unmapped user: FCR role claims are not trusted.
            client.DefaultRequestHeaders.Add("X-ERMS-Subject", "erms-subject-unknown");
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/requests")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-ERMS-Subject");

            // No ERMS authentication: module API challenges with 401.
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.GetAsync("/api/requests")).StatusCode);

            // Stable ERMS subject is mapped to an internal FCR user and assigned roles.
            client.DefaultRequestHeaders.Add("X-ERMS-Subject", "erms-subject-known");
            var submit = await client.PostAsJsonAsync("/api/requests",
                new { subject = "ERMS-hosted request", justification = "Integration test",
                    description = "Exercise FCR inside the ERMS host pipeline." });
            Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            try { Directory.Delete(dataDir, recursive: true); } catch { }
        }
    }

    private sealed class ErmsTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-ERMS-Subject", out var subject)
                || string.IsNullOrWhiteSpace(subject))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim("sub", subject.ToString())], Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
