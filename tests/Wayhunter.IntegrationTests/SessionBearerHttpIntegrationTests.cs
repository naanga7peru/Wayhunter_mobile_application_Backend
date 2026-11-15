
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;


namespace Wayhunter.IntegrationTests;

public sealed class SessionBearerHttpIntegrationTests
{
    private const string TestSecret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    private sealed class FakeSessionReader
        : IAuthenticatedSessionReader
    {
        public UserAccount? Account { get; set; }
        public IdentitySession? Session { get; set; }

        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Account);
        }

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Session);
        }
    }

    private sealed class TestData
    {
        public UserAccount Account { get; }
        public IdentitySession Session { get; }
        public FakeSessionReader Reader { get; }
        public SignedSessionTokenService Tokens { get; }
        public string Token { get; }

        public TestData()
        {
            var now = DateTimeOffset.UtcNow;

            Account = UserAccount.RegisterPending(
                "Wayhunter Traveler",
                "test@example.com",
                "test-password-hash",
                now.AddHours(-2));

            Account.ActivateAfterVerifiedOtp(
                now.AddHours(-1));

            Session = IdentitySession.Create(
                Account.UserId,
                now.AddMinutes(-10),
                now.AddHours(1));

            Reader = new FakeSessionReader
            {
                Account = Account,
                Session = Session
            };

            Tokens = new SignedSessionTokenService(
                "wayhunter-http-test",
                "wayhunter-test-client",
                TestSecret);

            Token = Tokens.Issue(
                Account, Session, now);
        }
    }


    private static async Task<WebApplication> CreateServerAsync(
        TestData data)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development"
            });

        // Modern in-memory HTTP test server.
        builder.WebHost.UseTestServer();

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(data.Tokens);

        builder.Services.AddSingleton<IAuthenticatedSessionReader>(
            data.Reader);

        builder.Services.AddScoped<
            AuthenticatedSessionAuthorizationService>();

        builder.Services
            .AddAuthentication(
                SessionBearerAuthenticationHandler.SchemeName)
            .AddScheme<
                AuthenticationSchemeOptions,
                SessionBearerAuthenticationHandler>(
                SessionBearerAuthenticationHandler.SchemeName,
                _ => { });

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/protected", (HttpContext context) =>
        {
            return context.User.FindFirst("sub")?.Value
                ?? "missing-user";
        })
        .RequireAuthorization();

        await app.StartAsync();

        return app;
    }


    [Fact]
    public async Task MissingToken_Returns401()
    {
        var data = new TestData();

        await using var server = await CreateServerAsync(data);
        using var client = server.GetTestClient();

        var response = await client.GetAsync("/protected");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_Returns200()
    {
        var data = new TestData();

        await using var server = await CreateServerAsync(data);
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", data.Token);

        var response = await client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(data.Account.UserId, body);
    }

    [Fact]
    public async Task InvalidToken_Returns401()
    {
        var data = new TestData();

        await using var server = await CreateServerAsync(data);
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", "invalid.token.value");

        var response = await client.GetAsync("/protected");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task RevokedSession_Returns401()
    {
        var data = new TestData();

        data.Session.Revoke(DateTimeOffset.UtcNow);

        await using var server = await CreateServerAsync(data);
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", data.Token);

        var response = await client.GetAsync("/protected");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task SuspendedAccount_Returns401()
    {
        var data = new TestData();

        data.Account.Suspend();

        await using var server = await CreateServerAsync(data);
        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", data.Token);

        var response = await client.GetAsync("/protected");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}
