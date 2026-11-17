
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SessionBearerAuthenticationHandlerTests
{
    private const string Secret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    private sealed class FakeReader : IAuthenticatedSessionReader
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

    private sealed class TestSetup : IDisposable
    {
        public ServiceProvider Provider { get; }
        public FakeReader Reader { get; }
        public UserAccount Account { get; }
        public IdentitySession Session { get; }
        public string Token { get; }

        public TestSetup()
        {
            var now = DateTimeOffset.UtcNow;

            Account = UserAccount.RegisterPending(
                "Test Traveler",
                "traveler@example.com",
                "test-hash",
                now.AddHours(-2));

            Account.ActivateAfterVerifiedOtp(
                now.AddHours(-1));

            Session = IdentitySession.Create(
                Account.UserId,
                now.AddMinutes(-10),
                now.AddHours(1));

            Reader = new FakeReader
            {
                Account = Account,
                Session = Session
            };

            var tokens = new SignedSessionTokenService(
                "wayhunter-test",
                "wayhunter-mobile-test",
                Secret);

            Token = tokens.Issue(Account, Session, now);

            var services = new ServiceCollection();

            services.AddLogging();

            services.AddSingleton<UrlEncoder>(
                UrlEncoder.Default);

            services.AddSingleton<TimeProvider>(
                TimeProvider.System);

            services.AddSingleton(tokens);

            services.AddSingleton(Reader);

            services.AddSingleton<IAuthenticatedSessionReader>(
                Reader);

            services.AddScoped<
                AuthenticatedSessionAuthorizationService>();

            services
                .AddAuthentication(
                    SessionBearerAuthenticationHandler.SchemeName)
                .AddScheme<
                    AuthenticationSchemeOptions,
                    SessionBearerAuthenticationHandler>(
                    SessionBearerAuthenticationHandler.SchemeName,
                    _ => { });

            Provider = services.BuildServiceProvider();
        }

        public async Task<AuthenticateResult> AuthenticateAsync(
            string? authorizationHeader)
        {
            using var scope = Provider.CreateScope();

            var context = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider
            };

            if (authorizationHeader is not null)
            {
                context.Request.Headers.Authorization =
                    authorizationHeader;
            }

            return await context.AuthenticateAsync(
                SessionBearerAuthenticationHandler.SchemeName);
        }

        public void Dispose()
        {
            Provider.Dispose();
        }
    }

    [Fact]
    public async Task ValidBearerToken_AuthenticatesUser()
    {
        using var setup = new TestSetup();

        var result = await setup.AuthenticateAsync(
            "Bearer " + setup.Token);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);

        Assert.Equal(
            setup.Account.UserId,
            result.Principal.FindFirst("sub")?.Value);

        Assert.Equal(
            setup.Session.SessionId,
            result.Principal.FindFirst("sid")?.Value);

        Assert.Equal(
            setup.Account.UserId,
            result.Principal.FindFirst(
                ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task MissingHeader_ReturnsNoResult()
    {
        using var setup = new TestSetup();

        var result = await setup.AuthenticateAsync(null);

        Assert.True(result.None);
    }

    [Fact]
    public async Task InvalidBearerToken_IsRejected()
    {
        using var setup = new TestSetup();

        var result = await setup.AuthenticateAsync(
            "Bearer invalid.token.value");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task BasicAuthentication_IsRejected()
    {
        using var setup = new TestSetup();

        var result = await setup.AuthenticateAsync(
            "Basic abc123");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task RevokedSession_IsRejected()
    {
        using var setup = new TestSetup();

        setup.Session.Revoke(DateTimeOffset.UtcNow);

        var result = await setup.AuthenticateAsync(
            "Bearer " + setup.Token);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SuspendedAccount_IsRejected()
    {
        using var setup = new TestSetup();

        setup.Account.Suspend();

        var result = await setup.AuthenticateAsync(
            "Bearer " + setup.Token);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MissingSession_IsRejected()
    {
        using var setup = new TestSetup();

        setup.Reader.Session = null;

        var result = await setup.AuthenticateAsync(
            "Bearer " + setup.Token);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MissingAccount_IsRejected()
    {
        using var setup = new TestSetup();

        setup.Reader.Account = null;

        var result = await setup.AuthenticateAsync(
            "Bearer " + setup.Token);

        Assert.False(result.Succeeded);
    }
}
