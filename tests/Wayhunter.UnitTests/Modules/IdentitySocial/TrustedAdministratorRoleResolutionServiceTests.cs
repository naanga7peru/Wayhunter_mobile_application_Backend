using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class TrustedAdministratorRoleResolutionServiceTests
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
            => Task.FromResult(Account);

        public Task<IdentitySession?> FindSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Session);
    }

    private sealed class FakeRoleReader
        : ITrustedAdministratorRoleReader
    {
        public AdministratorRoleAssignment? Assignment { get; set; }
        public bool ThrowOnRead { get; set; }
        public int ReadCount { get; private set; }

        public Task<AdministratorRoleAssignment?> FindAssignmentAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;

            if (ThrowOnRead)
                throw new InvalidOperationException(
                    "Role source unavailable.");

            return Task.FromResult(Assignment);
        }
    }

    private sealed record TestContext(
        TrustedAdministratorRoleResolutionService Resolver,
        FakeRoleReader Roles,
        UserAccount Account,
        IdentitySession Session,
        string Token);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
            => _utcNow;
    }

    private static TestContext CreateContext(
        TimeProvider? clock = null)
    {
        var now = DateTimeOffset.UtcNow;

        var account = UserAccount.RegisterPending(
            "Admin Candidate",
            "admin-candidate@example.com",
            "test-password-hash",
            now.AddHours(-2));

        account.ActivateAfterVerifiedOtp(
            now.AddHours(-1));

        var session = IdentitySession.Create(
            account.UserId,
            now.AddMinutes(-5),
            now.AddMinutes(30));

        var tokens = new SignedSessionTokenService(
            "wayhunter-test",
            "wayhunter-mobile-test",
            TestSecret);

        var token = tokens.Issue(
            account,
            session,
            now);

        var sessions = new FakeSessionReader
        {
            Account = account,
            Session = session
        };

        var roles = new FakeRoleReader
        {
            Assignment = new AdministratorRoleAssignment(
                account.UserId,
                "Admin",
                AdministratorRoleAssignmentStatus.Active)
        };

        var sessionAuthorization =
            new AuthenticatedSessionAuthorizationService(
                tokens,
                sessions,
                clock ?? TimeProvider.System);

        var resolver =
            new TrustedAdministratorRoleResolutionService(
                sessionAuthorization,
                roles);

        return new TestContext(
            resolver,
            roles,
            account,
            session,
            token);
    }

    [Fact]
    public async Task ValidAdminAssignment_ReturnsApprovedRoleClaim()
    {
        var context = CreateContext();

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.NotNull(claim);
        Assert.Equal(ClaimTypes.Role, claim.Type);
        Assert.Equal("Admin", claim.Value);
        Assert.Equal(1, context.Roles.ReadCount);
    }

    [Fact]
    public async Task MissingAssignment_DoesNotGrantAdmin()
    {
        var context = CreateContext();
        context.Roles.Assignment = null;

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
    }

    [Theory]
    [InlineData(AdministratorRoleAssignmentStatus.Revoked)]
    [InlineData(AdministratorRoleAssignmentStatus.Inactive)]
    public async Task NonActiveAssignment_DoesNotGrantAdmin(
        AdministratorRoleAssignmentStatus status)
    {
        var context = CreateContext();

        context.Roles.Assignment =
            new AdministratorRoleAssignment(
                context.Account.UserId,
                "Admin",
                status);

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Administrator")]
    [InlineData("")]
    public async Task UnapprovedRole_DoesNotGrantAdmin(
        string role)
    {
        var context = CreateContext();

        context.Roles.Assignment =
            new AdministratorRoleAssignment(
                context.Account.UserId,
                role,
                AdministratorRoleAssignmentStatus.Active);

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
    }

    [Theory]
    [InlineData("invalid-user-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidAssignmentUserId_DoesNotGrantAdmin(
        string assignedUserId)
    {
        var context = CreateContext();

        context.Roles.Assignment =
            new AdministratorRoleAssignment(
                assignedUserId,
                "Admin",
                AdministratorRoleAssignmentStatus.Active);

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
    }

    [Fact]
    public async Task OtherUsersAssignment_DoesNotGrantAdmin()
    {
        var context = CreateContext();

        context.Roles.Assignment =
            new AdministratorRoleAssignment(
                Guid.NewGuid().ToString("D"),
                "Admin",
                AdministratorRoleAssignmentStatus.Active);

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
    }

    [Fact]
    public async Task UnavailableRoleSource_FailsClosed()
    {
        var context = CreateContext();
        context.Roles.ThrowOnRead = true;

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
    }

    [Fact]
    public async Task InvalidJwt_DoesNotGrantAdmin()
    {
        var context = CreateContext();

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync("not.a.valid.jwt");

        Assert.Null(claim);
        Assert.Equal(0, context.Roles.ReadCount);
    }

    [Fact]
    public async Task MissingBearerToken_DoesNotGrantAdmin()
    {
        var context = CreateContext();

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(null);

        Assert.Null(claim);
        Assert.Equal(0, context.Roles.ReadCount);
    }

    [Fact]
    public async Task SuspendedAccount_DoesNotGrantAdmin()
    {
        var context = CreateContext();
        context.Account.Suspend();

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
        Assert.Equal(0, context.Roles.ReadCount);
    }

    [Fact]
    public async Task RevokedSession_DoesNotGrantAdmin()
    {
        var context = CreateContext();
        context.Session.Revoke(DateTimeOffset.UtcNow);

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
        Assert.Equal(0, context.Roles.ReadCount);
    }

    [Fact]
    public async Task UnverifiedAccount_DoesNotGrantAdmin()
    {
        var now = DateTimeOffset.UtcNow;

        var pendingAccount = UserAccount.RegisterPending(
            "Pending Administrator",
            "pending-admin@example.com",
            "test-password-hash",
            now.AddHours(-2));

        var session = IdentitySession.Create(
            pendingAccount.UserId,
            now.AddMinutes(-5),
            now.AddMinutes(30));

        var tokens = new SignedSessionTokenService(
            "wayhunter-test",
            "wayhunter-mobile-test",
            TestSecret);

        // Synthetic signed JWT for defense-in-depth testing.
        // Normal production token issuance rejects pending accounts.
        var jwt = new JwtSecurityToken(
            issuer: "wayhunter-test",
            audience: "wayhunter-mobile-test",
            claims:
            [
                new Claim("sub", pendingAccount.UserId),
                new Claim("sid", session.SessionId),
                new Claim("jti", Guid.NewGuid().ToString("D"))
            ],
            notBefore: now.UtcDateTime.AddMinutes(-1),
            expires: session.ExpiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(TestSecret)),
                SecurityAlgorithms.HmacSha256));

        var token = new JwtSecurityTokenHandler()
            .WriteToken(jwt);

        // Confirm the JWT itself is valid.
        Assert.NotNull(tokens.Validate(token));

        var sessions = new FakeSessionReader
        {
            Account = pendingAccount,
            Session = session
        };

        var roles = new FakeRoleReader
        {
            Assignment = new AdministratorRoleAssignment(
                pendingAccount.UserId,
                "Admin",
                AdministratorRoleAssignmentStatus.Active)
        };

        var authorization =
            new AuthenticatedSessionAuthorizationService(
                tokens,
                sessions,
                TimeProvider.System);

        var resolver =
            new TrustedAdministratorRoleResolutionService(
                authorization,
                roles);

        var claim = await resolver.ResolveAdminRoleClaimAsync(
            token);

        Assert.Null(claim);
        Assert.Equal(0, roles.ReadCount);
    }

    [Fact]
    public async Task DeletionRequestedAccount_DoesNotGrantAdmin()
    {
        var context = CreateContext();

        context.Account.RequestDeletion(
            DateTimeOffset.UtcNow);

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
        Assert.Equal(0, context.Roles.ReadCount);
    }

    [Fact]
    public async Task ExpiredPersistedSession_DoesNotGrantAdmin()
    {
        // Advance the authorization clock beyond the stored
        // session expiry without waiting for real time to pass.
        var future = DateTimeOffset.UtcNow.AddHours(2);

        var context = CreateContext(
            new FixedTimeProvider(future));

        var claim = await context.Resolver
            .ResolveAdminRoleClaimAsync(context.Token);

        Assert.Null(claim);
        Assert.Equal(0, context.Roles.ReadCount);
    }
}
