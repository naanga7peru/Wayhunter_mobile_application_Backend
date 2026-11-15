
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Controllers.Mobile.IdentitySocial;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.IntegrationTests;

public sealed class SessionLogoutHttpIntegrationTests
{
    private const string TestSecret =
        "test-only-jwt-secret-at-least-32-characters-long-123";

    private sealed class FakeSessionStore :
        IAuthenticatedSessionReader,
        IIdentitySessionRevocationStore
    {
        private readonly object _gate = new();

        public UserAccount Account { get; }
        public IdentitySession Session { get; }

        public FakeSessionStore(
            UserAccount account,
            IdentitySession session)
        {
            Account = account;
            Session = session;
        }

        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            UserAccount? result =
                Account.UserId == userId ? Account : null;

            return Task.FromResult(result);
        }

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IdentitySession? result =
                Session.SessionId == sessionId
                    ? Session
                    : null;

            return Task.FromResult(result);
        }

        public Task<IdentitySession?> FindAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            return FindSessionAsync(
                sessionId, cancellationToken);
        }

        public Task<SessionRevocationWriteResult> TryRevokeAsync(
            string userId,
            string sessionId,
            DateTimeOffset revokedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                if (Session.UserId != userId ||
                    Session.SessionId != sessionId)
                {
                    return Task.FromResult(
                        SessionRevocationWriteResult.NotFound);
                }

                if (Session.RevokedAtUtc.HasValue)
                {
                    return Task.FromResult(
                        SessionRevocationWriteResult.AlreadyRevoked);
                }

                Session.Revoke(revokedAtUtc);

                return Task.FromResult(
                    SessionRevocationWriteResult.Revoked);
            }
        }
    }

    private static async Task<(
        WebApplication App,
        string Token)> CreateServerAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var account = UserAccount.RegisterPending(
            "Test Traveler",
            "logout@example.com",
            "test-hash",
            now.AddHours(-2));

        account.ActivateAfterVerifiedOtp(
            now.AddHours(-1));

        var session = IdentitySession.Create(
            account.UserId,
            now.AddMinutes(-10),
            now.AddHours(1));

        var store = new FakeSessionStore(
            account, session);

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });

        builder.WebHost.UseTestServer();

        builder.Configuration[
            "Authentication:Jwt:Issuer"] =
            "wayhunter-logout-test";

        builder.Configuration[
            "Authentication:Jwt:Audience"] =
            "wayhunter-mobile-test";

        builder.Configuration[
            "Authentication:Jwt:SigningSecret"] =
            TestSecret;

        builder.Services
            .AddControllers()
            .AddApplicationPart(
                typeof(SessionLogoutController).Assembly);

        builder.Services.AddSingleton(
            TimeProvider.System);

        builder.Services.AddSingleton<
            IAuthenticatedSessionReader>(store);

        builder.Services.AddSingleton<
            IIdentitySessionRevocationStore>(store);

        builder.Services.AddScoped<
            SessionLogoutService>();

        builder.Services
            .AddWayhunterSessionAuthentication(
                builder.Configuration);

        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.MapGet("/protected", () => "access-granted")
            .RequireAuthorization();

        await app.StartAsync();

        var tokens = app.Services
            .GetRequiredService<SignedSessionTokenService>();

        var token = tokens.Issue(
            account, session, now);

        return (app, token);
    }

    [Fact]
    public async Task Logout_RevokesToken_AndBlocksReuse()
    {
        var (app, token) = await CreateServerAsync();

        await using (app)
        {
            using var client = app.GetTestClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", token);

            // Initially the JWT must be accepted.
            var before = await client.GetAsync(
                "/protected");

            Assert.Equal(
                HttpStatusCode.OK,
                before.StatusCode);

            // Logout via the real API controller.
            var logout = await client.PostAsync(
                "/api/mobile/auth/logout",
                content: null);

            Assert.Equal(
                HttpStatusCode.NoContent,
                logout.StatusCode);

            // The same JWT must now be rejected.
            var after = await client.GetAsync(
                "/protected");

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                after.StatusCode);
        }
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns401()
    {
        var (app, _) = await CreateServerAsync();

        await using (app)
        {
            using var client = app.GetTestClient();

            var response = await client.PostAsync(
                "/api/mobile/auth/logout",
                content: null);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task Logout_WithInvalidToken_Returns401()
    {
        var (app, _) = await CreateServerAsync();

        await using (app)
        {
            using var client = app.GetTestClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    "invalid.token.value");

            var response = await client.PostAsync(
                "/api/mobile/auth/logout",
                content: null);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }
    }
}
