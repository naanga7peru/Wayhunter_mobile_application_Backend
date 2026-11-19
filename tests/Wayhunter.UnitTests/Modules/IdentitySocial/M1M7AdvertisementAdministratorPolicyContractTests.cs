using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Wayhunter.Api.Authorization;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class M1M7AdvertisementAdministratorPolicyContractTests
{
    private const string Secret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    private sealed class FakeSessionReader
        : IAuthenticatedSessionReader
    {
        public UserAccount? Account { get; set; }
        public IdentitySession? Session { get; set; }

        public Task<UserAccount?> FindAccountAsync(
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Account);

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Session);
    }

    private sealed class TestHarness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public UserAccount Account { get; }
        public IdentitySession Session { get; }
        public string Token { get; }
        public SignedSessionTokenService Tokens { get; }

        public TestHarness(string? configuredRole = "Admin")
        {
            var now = DateTimeOffset.UtcNow;

            Account = UserAccount.RegisterPending(
                "M1 M7 Contract User",
                "m1-m7-contract@example.com",
                "test-password-hash",
                now.AddHours(-2));

            Account.ActivateAfterVerifiedOtp(
                now.AddHours(-1));

            Session = IdentitySession.Create(
                Account.UserId,
                now.AddMinutes(-5),
                now.AddMinutes(30));

            Tokens = new SignedSessionTokenService(
                "wayhunter-test",
                "wayhunter-mobile-test",
                Secret);

            Token = Tokens.Issue(Account, Session, now);

            var values = new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] =
                    "wayhunter-test",
                ["Authentication:Jwt:Audience"] =
                    "wayhunter-mobile-test",
                ["Authentication:Jwt:SigningSecret"] =
                    Secret,
                ["Identity:AdministratorRole"] =
                    configuredRole
            };

            var configuration =
                new ConfigurationBuilder()
                    .AddInMemoryCollection(values)
                    .Build();

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton<UrlEncoder>(
                UrlEncoder.Default);
            services.AddSingleton<TimeProvider>(
                TimeProvider.System);

            services.AddSingleton<IAuthenticatedSessionReader>(
                new FakeSessionReader
                {
                    Account = Account,
                    Session = Session
                });

            // Real M1 authentication registration.
            services.AddWayhunterSessionAuthentication(
                configuration);

            // Real M7 Administrator authorization policy.
            services.AddAdvertisementAdministratorPolicy(
                configuration);

            _provider = services.BuildServiceProvider();
        }

        public async Task<AuthenticateResult> AuthenticateAsync(
            string token)
        {
            using var scope = _provider.CreateScope();

            var context = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider
            };

            context.Request.Headers.Authorization =
                "Bearer " + token;

            return await context.AuthenticateAsync(
                SessionBearerAuthenticationHandler.SchemeName);
        }

        public async Task<bool> AuthorizesAdminAsync(
            ClaimsPrincipal principal)
        {
            using var scope = _provider.CreateScope();

            var authorization = scope.ServiceProvider
                .GetRequiredService<IAuthorizationService>();

            var result = await authorization.AuthorizeAsync(
                principal,
                null,
                AdvertisementAdministratorPolicy.Name);

            return result.Succeeded;
        }

        public ClaimsPrincipal CreateSimulatedTrustedAdmin(
            string? subjectOverride = null,
            bool authenticated = true)
        {
            var claims = new[]
            {
                new Claim(
                    "sub",
                    subjectOverride ?? Account.UserId),
                new Claim(
                    ClaimTypes.NameIdentifier,
                    Account.UserId),
                new Claim(
                    ClaimTypes.Role,
                    "Admin")
            };

            return new ClaimsPrincipal(
                new ClaimsIdentity(
                    claims,
                    authenticated ? "TestTrustedAdmin" : null));
        }

        public string CreateJwtWithInjectedAdminRole()
        {
            var now = DateTimeOffset.UtcNow;

            var jwt = new JwtSecurityToken(
                issuer: "wayhunter-test",
                audience: "wayhunter-mobile-test",
                claims:
                [
                    new Claim("sub", Account.UserId),
                    new Claim("sid", Session.SessionId),
                    new Claim(
                        "jti",
                        Guid.NewGuid().ToString("D")),
                    new Claim(ClaimTypes.Role, "Admin")
                ],
                notBefore: now.UtcDateTime.AddMinutes(-1),
                expires: Session.ExpiresAtUtc.UtcDateTime,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(Secret)),
                    SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler()
                .WriteToken(jwt);
        }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }

    [Fact]
    public async Task ValidM1Bearer_DoesNotGrantM7AdminAccess()
    {
        using var harness = new TestHarness();

        var authentication =
            await harness.AuthenticateAsync(harness.Token);

        Assert.True(authentication.Succeeded);
        Assert.NotNull(authentication.Principal);

        var principal = authentication.Principal;

        Assert.False(principal.IsInRole("Admin"));

        Assert.False(
            await harness.AuthorizesAdminAsync(principal));
    }

    [Fact]
    public async Task InjectedJwtAdminRole_IsIgnoredByM1AndM7()
    {
        using var harness = new TestHarness();

        var injectedToken =
            harness.CreateJwtWithInjectedAdminRole();

        // The JWT signature is valid, but a token-supplied
        // role must not become a trusted Administrator role.
        Assert.NotNull(
            harness.Tokens.Validate(injectedToken));

        var authentication =
            await harness.AuthenticateAsync(injectedToken);

        Assert.True(authentication.Succeeded);
        Assert.NotNull(authentication.Principal);

        Assert.Empty(
            authentication.Principal.FindAll(
                ClaimTypes.Role));

        Assert.False(
            await harness.AuthorizesAdminAsync(
                authentication.Principal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Administrator")]
    [InlineData("admin")]
    public async Task InvalidAdminRoleConfiguration_FailsClosed(
        string? configuredRole)
    {
        using var harness =
            new TestHarness(configuredRole);

        // Simulated trusted role principal is test-only.
        // Production role activation is not enabled.
        var principal =
            harness.CreateSimulatedTrustedAdmin();

        Assert.False(
            await harness.AuthorizesAdminAsync(principal));
    }

    [Fact]
    public async Task ApprovedTrustedAdminPrincipal_MatchesPolicy()
    {
        using var harness = new TestHarness();

        // Positive contract test, not production Admin issuance.
        var principal =
            harness.CreateSimulatedTrustedAdmin();

        Assert.True(
            await harness.AuthorizesAdminAsync(principal));
    }

    [Fact]
    public async Task MismatchedIdentityClaims_DenyAdminAccess()
    {
        using var harness = new TestHarness();

        var principal = harness.CreateSimulatedTrustedAdmin(
            Guid.NewGuid().ToString("D"));

        Assert.False(
            await harness.AuthorizesAdminAsync(principal));
    }

    [Fact]
    public async Task AnonymousAdminClaim_DeniesAdminAccess()
    {
        using var harness = new TestHarness();

        var principal = harness.CreateSimulatedTrustedAdmin(
            authenticated: false);

        Assert.False(
            await harness.AuthorizesAdminAsync(principal));
    }
}