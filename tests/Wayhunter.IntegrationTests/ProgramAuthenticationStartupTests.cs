
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;




namespace Wayhunter.IntegrationTests;

public sealed class ProgramAuthenticationStartupTests
{
    private sealed class RejectAllSessionReader
        : IAuthenticatedSessionReader
    {
        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<UserAccount?>(null);
        }

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IdentitySession?>(null);
        }
    }


    private sealed class StartupAuthenticationFactory
        : WebApplicationFactory<Program>
    {

        private readonly bool _enabled;

        public StartupAuthenticationFactory(bool enabled = true)
        {
            _enabled = enabled;
        }

        protected override IHost CreateHost(
            IHostBuilder builder)
        {
            // Host configuration reaches Program.cs
            // before WebApplication.CreateBuilder(args).
            builder.ConfigureHostConfiguration(configuration =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Identity:EnableSharedAuthentication"] =
                        _enabled ? "true" : "false",

                        ["ConnectionStrings:WayhunterSharedDb"] =
                            "Server=localhost;" +
                            "Database=WayhunterAuthStartupTest;" +
                            "Trusted_Connection=True;" +
                            "TrustServerCertificate=True",

                        ["Authentication:Jwt:Issuer"] =
                            "wayhunter-startup-test",

                        ["Authentication:Jwt:Audience"] =
                            "wayhunter-mobile-test",

                        ["Authentication:Jwt:SigningSecret"] =
                            "test-only-startup-signing-secret-123456789"
                    });
            });

            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                // Test-only reader. No SQL queries.
                services.AddScoped<
                    IAuthenticatedSessionReader,
                    RejectAllSessionReader>();
            });
        }
    }


    private static WebApplicationFactory<Program> CreateFactory(
        bool enabled = true)
    {
        return new StartupAuthenticationFactory(enabled);
    }



    [Theory]
    [InlineData(
        "/api/mobile/coins-wallet/me")]
    [InlineData(
        "/api/mobile/trip-plans/11111111-1111-1111-1111-111111111111")]
    public async Task ActualProgram_AnonymousGet_Returns401(
        string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ActualProgram_AnonymousGpsConsent_Returns401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var tripId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var response = await client.PutAsJsonAsync(
            $"/api/mobile/trips/{tripId}/sessions/" +
            $"{sessionId}/gps-consent",
            new { isGranted = true });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ActualProgram_InvalidBearerToken_Returns401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "invalid.token.value");

        var response = await client.GetAsync(
            "/api/mobile/coins-wallet/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("/api/mobile/coins-wallet/me")]
    [InlineData(
        "/api/mobile/trip-plans/11111111-1111-1111-1111-111111111111")]
    public async Task AuthenticationDisabled_ProtectedGet_Returns401(
        string path)
    {
        using var factory = CreateFactory(enabled: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthenticationDisabled_GpsConsent_Returns401()
    {
        using var factory = CreateFactory(enabled: false);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/mobile/trips/{Guid.NewGuid()}/sessions/" +
            $"{Guid.NewGuid()}/gps-consent",
            new { isGranted = true });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthenticationDisabled_BearerToken_Returns401()
    {
        using var factory = CreateFactory(enabled: false);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "any.token.value");

        var response = await client.GetAsync(
            "/api/mobile/coins-wallet/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthenticationDisabled_Logout_Returns401()
    {
        using var factory = CreateFactory(enabled: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/mobile/auth/logout",
            content: null);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

}
