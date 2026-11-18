
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Wayhunter.Api.Authorization;
using Wayhunter.Api.Controllers.Admin.HomeAdsAnalytics;

using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;

using Xunit;

namespace Wayhunter.IntegrationTests;

public sealed class AdvertisementAuthorizationHttpTests
{
    private const string TestScheme = "M7TestAuthentication";

    // ==========================================
    // Test-only Authentication Handler
    // ==========================================

    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult>
            HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(
                    "X-Test-User",
                    out var userHeader) ||
                userHeader.ToString() != "traveler")
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    Guid.NewGuid().ToString("D"))
            };

            var identity = new ClaimsIdentity(
                claims,
                TestScheme);

            var principal = new ClaimsPrincipal(identity);

            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(
                        principal,
                        TestScheme)));
        }
    }

    // ==========================================
    // Test-only Advertisement Repository
    // ==========================================

    private sealed class FakeAdvertisementRepository :
        IAdvertisementRepository,
        IAdvertisementWriteRepository
    {
        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AdvertisementDto> advertisements = [];

            return Task.FromResult(advertisements);
        }

        public Task<AdvertisementDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AdvertisementDto?>(null);
        }

        public Task<AdvertisementDto> CreateAsync(
            AdvertisementCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Test repository does not perform database writes.");
        }

        public Task<AdvertisementDto?> UpdateAsync(
            Guid id,
            AdvertisementUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Test repository does not perform database writes.");
        }

        public Task<AdvertisementDto?> ChangeStatusAsync(
            Guid id,
            AdvertisementStatusUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Test repository does not perform database writes.");
        }
    }

    // ==========================================
    // Test Server Setup
    // ==========================================

    private static async Task<WebApplication> CreateServerAsync()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });

        builder.WebHost.UseTestServer();

        // Approved M1/M7 Administrator role contract.
        // Used only for authorization verification.
        builder.Configuration[
            "Identity:AdministratorRole"] = "Admin";

        builder.Services
            .AddControllers()
            .AddApplicationPart(
                typeof(AdvertisementsController).Assembly);

        // Test-only authentication.
        // Production authentication remains unchanged.
        builder.Services
            .AddAuthentication(TestScheme)
            .AddScheme<
                AuthenticationSchemeOptions,
                TestAuthenticationHandler>(
                TestScheme,
                _ => { });

        // ==========================================
        // M7 - Centralized Administrator Policy
        // ==========================================

        // Register the same policy used by Program.cs.
        // Without this registration, Admin endpoints
        // throw a policy-not-found exception (HTTP 500).

        builder.Services.AddAdvertisementAdministratorPolicy(
            builder.Configuration);

        // ==========================================
        // M7 Test Dependency Injection
        // ==========================================

        builder.Services.AddScoped<FakeAdvertisementRepository>();

        builder.Services.AddScoped<IAdvertisementRepository>(
            sp => sp.GetRequiredService<
                FakeAdvertisementRepository>());

        builder.Services.AddScoped<IAdvertisementWriteRepository>(
            sp => sp.GetRequiredService<
                FakeAdvertisementRepository>());

        builder.Services.AddScoped<AdvertisementService>();

        builder.Services.AddSingleton(TimeProvider.System);

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        await app.StartAsync();

        return app;
    }

    // ==========================================
    // Anonymous Access - 401
    // ==========================================

    [Theory]
    [InlineData("/api/admin/advertisements")]
    [InlineData("/api/mobile/advertisements/active")]
    public async Task AnonymousGet_Returns401(
        string path)
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        var response = await client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ==========================================
    // Normal User - Admin API - 403
    // ==========================================

    [Fact]
    public async Task NormalUser_AdminGet_Returns403()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "traveler");

        var response = await client.GetAsync(
            "/api/admin/advertisements");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // ==========================================
    // Anonymous Admin Create - 401
    // ==========================================

    [Fact]
    public async Task Anonymous_AdminCreate_Returns401()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        var response = await client.PostAsync(
            "/api/admin/advertisements",
            content: null);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ==========================================
    // Unsupported Delete - 405
    // ==========================================

    [Fact]
    public async Task NormalUser_AdminDeleteOrUnknownAction_IsNotExposed()
    {
        await using var server = await CreateServerAsync();
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "traveler");

        var response = await client.DeleteAsync(
            $"/api/admin/advertisements/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.MethodNotAllowed,
            response.StatusCode);
    }
}
