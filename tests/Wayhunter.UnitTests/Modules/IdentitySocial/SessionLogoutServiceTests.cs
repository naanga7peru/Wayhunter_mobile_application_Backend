
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SessionLogoutServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeStore : IIdentitySessionRevocationStore
    {
        private readonly object _gate = new();

        public IdentitySession? Session { get; set; }

        public int RevokeCalls { get; private set; }

        public Task<IdentitySession?> FindAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                return Task.FromResult(
                    Session is not null &&
                    Session.SessionId == sessionId
                        ? Session
                        : null);
            }
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
                RevokeCalls++;

                if (Session is null ||
                    Session.UserId != userId ||
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

    private static (
        FakeStore Store,
        SessionLogoutService Service,
        IdentitySession Session) Setup()
    {
        var userId = Guid.NewGuid().ToString("D");

        var session = IdentitySession.Create(
            userId,
            Now.AddMinutes(-10),
            Now.AddHours(1));

        var store = new FakeStore { Session = session };

        return (
            store,
            new SessionLogoutService(store),
            session);
    }

    [Fact]
    public async Task ValidLogout_RevokesSession()
    {
        var (store, service, session) = Setup();

        var result = await service.LogoutAsync(
            session.UserId,
            session.SessionId,
            Now);

        Assert.True(result);
        Assert.Equal(Now, session.RevokedAtUtc);
        Assert.False(session.IsActiveAt(Now));
        Assert.Equal(1, store.RevokeCalls);
    }

    [Fact]
    public async Task LogoutTwice_IsIdempotent()
    {
        var (_, service, session) = Setup();

        Assert.True(await service.LogoutAsync(
            session.UserId, session.SessionId, Now));

        Assert.True(await service.LogoutAsync(
            session.UserId,
            session.SessionId,
            Now.AddMinutes(1)));

        Assert.Equal(Now, session.RevokedAtUtc);
    }

    [Fact]
    public async Task DifferentUser_CannotRevokeSession()
    {
        var (store, service, session) = Setup();

        var result = await service.LogoutAsync(
            Guid.NewGuid().ToString("D"),
            session.SessionId,
            Now);

        Assert.False(result);
        Assert.Null(session.RevokedAtUtc);
        Assert.Equal(0, store.RevokeCalls);
    }

    [Fact]
    public async Task DifferentSessionId_IsRejected()
    {
        var (store, service, session) = Setup();

        var result = await service.LogoutAsync(
            session.UserId,
            Guid.NewGuid().ToString("D"),
            Now);

        Assert.False(result);
        Assert.Equal(0, store.RevokeCalls);
    }

    [Fact]
    public async Task InvalidUserId_IsRejected()
    {
        var (_, service, session) = Setup();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.LogoutAsync(
                "invalid",
                session.SessionId,
                Now));
    }

    [Fact]
    public async Task InvalidSessionId_IsRejected()
    {
        var (_, service, session) = Setup();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.LogoutAsync(
                session.UserId,
                "invalid",
                Now));
    }

    [Fact]
    public async Task NonUtcTime_IsRejected()
    {
        var (_, service, session) = Setup();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.LogoutAsync(
                session.UserId,
                session.SessionId,
                Now.ToOffset(TimeSpan.FromHours(5.5))));
    }

    [Fact]
    public async Task CancelledRequest_IsRejected()
    {
        var (_, service, session) = Setup();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.LogoutAsync(
                session.UserId,
                session.SessionId,
                Now,
                cancellation.Token));
    }

    [Fact]
    public async Task FutureSession_IsRejected()
    {
        var (store, service, session) = Setup();

        var result = await service.LogoutAsync(
            session.UserId,
            session.SessionId,
            Now.AddHours(-1));

        Assert.False(result);
        Assert.Equal(0, store.RevokeCalls);
    }

    [Fact]
    public void NullStore_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionLogoutService(null!));
    }
}
