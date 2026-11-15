
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Wayhunter.Api.Controllers.Mobile.TravelPlanning;
using Wayhunter.Api.Extensions;
using Xunit;
using Wayhunter.Api.Services.IdentitySocial;

namespace Wayhunter.IntegrationTests;

public sealed class CrossModuleAuthorizationHttpTests
{
    [Theory]
    [InlineData("/api/mobile/coins-wallet/me")]
    [InlineData("/api/mobile/trip-plans/11111111-1111-1111-1111-111111111111")]
    public async Task ProtectedGet_WithoutToken_Returns401(
        string path)
    {


        await using var app = await CreateServerAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GpsConsent_WithoutToken_Returns401()
    {
        await using var app = await CreateServerAsync();
        using var client = app.GetTestClient();

        var tripId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var path =
            $"/api/mobile/trips/{tripId}/sessions/" +
            $"{sessionId}/gps-consent";

        var response = await client.PutAsJsonAsync(
            path,
            new { isGranted = true });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }


    private sealed class RejectAllSessionReader
        : IAuthenticatedSessionReader
    {
        public Task<
            Wayhunter.Domain.Modules.IdentitySocial.Entities.UserAccount?>
            FindAccountAsync(
                string userId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<
                Wayhunter.Domain.Modules.IdentitySocial.Entities.UserAccount?>(
                null);
        }

        public Task<
            Wayhunter.Domain.Modules.IdentitySocial.Entities.IdentitySession?>
            FindSessionAsync(
                string sessionId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<
                Wayhunter.Domain.Modules.IdentitySocial.Entities.IdentitySession?>(
                null);
        }
    }


    private static async Task<WebApplication>
        CreateServerAsync()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });

        builder.WebHost.UseTestServer();

        builder.Configuration[
            "Authentication:Jwt:Issuer"] = "test-issuer";

        builder.Configuration[
            "Authentication:Jwt:Audience"] = "test-audience";

        // Test-only value, never production credentials.
        builder.Configuration[
            "Authentication:Jwt:SigningSecret"] =
            "test-only-cross-module-jwt-secret-123456789";

        builder.Services
            .AddControllers()
            .AddApplicationPart(
                typeof(TripPlansController).Assembly);


        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddSingleton<
            IAuthenticatedSessionReader,
            RejectAllSessionReader>();

        builder.Services.AddWayhunterSessionAuthentication(
            builder.Configuration);


        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        await app.StartAsync();

        return app;
    }
}
