
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class AuthenticatedSessionAuthorizationServiceTests
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

    private static (
        SignedSessionTokenService Tokens,
        FakeReader Reader,
        AuthenticatedSessionAuthorizationService Service,
        UserAccount Account,
        IdentitySession Session,
        string Token) Setup()
    {
        var now = DateTimeOffset.UtcNow;

        var account = UserAccount.RegisterPending(
            "Test Traveler",
            "traveler@example.com",
            "test-hash",
            now.AddHours(-2));

        account.ActivateAfterVerifiedOtp(
            now.AddHours(-1));

        var session = IdentitySession.Create(
            account.UserId,
            now.AddMinutes(-10),
            now.AddHours(1));

        var tokens = new SignedSessionTokenService(
            "wayhunter-test",
            "wayhunter-mobile-test",
            Secret);

        var reader = new FakeReader
        {
            Account = account,
            Session = session
        };

        var service =
            new AuthenticatedSessionAuthorizationService(
                tokens,
                reader,
                TimeProvider.System);

        var token = tokens.Issue(
            account, session, now);

        return (tokens, reader, service,
            account, session, token);
    }

    [Fact]
    public async Task ValidSession_IsAuthorized()
    {
        var data = Setup();

        var result = await data.Service.AuthorizeAsync(
            data.Token);

        Assert.NotNull(result);
        Assert.Equal(data.Account.UserId, result.UserId);
        Assert.Equal(data.Session.SessionId, result.SessionId);
    }

    [Fact]
    public async Task MissingToken_IsRejected()
    {
        var data = Setup();

        Assert.Null(
            await data.Service.AuthorizeAsync(null));
    }

    [Fact]
    public async Task InvalidToken_IsRejected()
    {
        var data = Setup();

        Assert.Null(
            await data.Service.AuthorizeAsync(
                "invalid.token.value"));
    }

    [Fact]
    public async Task RevokedSession_IsRejected()
    {
        var data = Setup();

        data.Session.Revoke(DateTimeOffset.UtcNow);

        Assert.Null(
            await data.Service.AuthorizeAsync(data.Token));
    }

    [Fact]
    public async Task SuspendedAccount_IsRejected()
    {
        var data = Setup();
        data.Account.Suspend();

        Assert.Null(
            await data.Service.AuthorizeAsync(data.Token));
    }

    [Fact]
    public async Task DeletionRequestedAccount_IsRejected()
    {
        var data = Setup();
        data.Account.RequestDeletion(DateTimeOffset.UtcNow);

        Assert.Null(
            await data.Service.AuthorizeAsync(data.Token));
    }

    [Fact]
    public async Task MissingAccount_IsRejected()
    {
        var data = Setup();
        data.Reader.Account = null;

        Assert.Null(
            await data.Service.AuthorizeAsync(data.Token));
    }

    [Fact]
    public async Task MissingSession_IsRejected()
    {
        var data = Setup();
        data.Reader.Session = null;

        Assert.Null(
            await data.Service.AuthorizeAsync(data.Token));
    }

    [Fact]
    public async Task DifferentSession_IsRejected()
    {
        var data = Setup();

        data.Reader.Session = IdentitySession.Create(
            data.Account.UserId,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1));

        Assert.Null(
            await data.Service.AuthorizeAsync(data.Token));
    }

    [Fact]
    public async Task DifferentAccount_IsRejected()
    {
        var data = Setup();

        var other = UserAccount.RegisterPending(
            "Another Traveler",
            "another@example.com",
            "test-hash",
            DateTimeOffset.UtcNow.AddHours(-2));

        other.ActivateAfterVerifiedOtp(
            DateTimeOffset.UtcNow.AddHours(-1));

        data.Reader.Account = other;

        Assert.Null(
            await data.Service.AuthorizeAsync(data.Token));
    }

    [Fact]
    public async Task Cancellation_IsRespected()
    {
        var data = Setup();

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => data.Service.AuthorizeAsync(
                data.Token,
                cancellation.Token));
    }
}
