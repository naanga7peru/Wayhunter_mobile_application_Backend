
using System.Security.Claims;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SessionTokenClaimsFactoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly SessionTokenClaimsFactory _factory = new();

    private static UserAccount ActiveAccount()
    {
        var account = UserAccount.RegisterPending(
            "Traveler",
            "traveler@example.com",
            "test-password-hash",
            Now.AddHours(-2));

        account.ActivateAfterVerifiedOtp(
            Now.AddHours(-1));

        return account;
    }

    private static IdentitySession Session(UserAccount account)
    {
        return IdentitySession.Create(
            account.UserId,
            Now.AddMinutes(-10),
            Now.AddHours(1));
    }


    private static string Value(
        IReadOnlyList<Claim> claims,
        string type)
    {
        return Assert.Single(
            claims,
            claim => claim.Type == type).Value;
    }


    [Fact]
    public void ValidSession_ProducesCorrectSubjectAndSessionId()
    {
        var account = ActiveAccount();
        var session = Session(account);

        var claims = _factory.Create(account, session, Now);

        Assert.Equal(account.UserId, Value(claims, "sub"));
        Assert.Equal(session.SessionId, Value(claims, "sid"));
        Assert.Equal(6, claims.Count);
    }

    [Fact]
    public void ClaimsContainCorrectTimeWindow()
    {
        var account = ActiveAccount();
        var session = Session(account);

        var claims = _factory.Create(account, session, Now);

        Assert.Equal(
            Now.ToUnixTimeSeconds().ToString(),
            Value(claims, "iat"));

        Assert.Equal(
            Now.ToUnixTimeSeconds().ToString(),
            Value(claims, "nbf"));

        Assert.Equal(
            session.ExpiresAtUtc.ToUnixTimeSeconds().ToString(),
            Value(claims, "exp"));
    }

    [Fact]
    public void EachIssuance_HasUniqueJti()
    {
        var account = ActiveAccount();
        var session = Session(account);

        var first = _factory.Create(account, session, Now);
        var second = _factory.Create(account, session, Now);

        Assert.NotEqual(
            Value(first, "jti"),
            Value(second, "jti"));
    }

    [Fact]
    public void PendingAccount_IsRejected()
    {
        var account = UserAccount.RegisterPending(
            "Pending",
            "pending@example.com",
            "hash",
            Now.AddHours(-2));

        var session = Session(account);

        Assert.Throws<InvalidOperationException>(() =>
            _factory.Create(account, session, Now));
    }

    [Fact]
    public void SuspendedAccount_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        account.Suspend();

        Assert.Throws<InvalidOperationException>(() =>
            _factory.Create(account, session, Now));
    }

    [Fact]
    public void OtherUsersSession_IsRejected()
    {
        var account = ActiveAccount();
        var other = ActiveAccount();
        var session = Session(other);

        Assert.Throws<InvalidOperationException>(() =>
            _factory.Create(account, session, Now));
    }

    [Fact]
    public void RevokedSession_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        session.Revoke(Now.AddMinutes(-1));

        Assert.Throws<InvalidOperationException>(() =>
            _factory.Create(account, session, Now));
    }

    [Fact]
    public void ExpiredSession_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.Throws<InvalidOperationException>(() =>
            _factory.Create(
                account, session, Now.AddHours(1)));
    }

    [Fact]
    public void NonUtcTime_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.Throws<ArgumentException>(() =>
            _factory.Create(
                account,
                session,
                Now.ToOffset(TimeSpan.FromHours(5.5))));
    }

    [Fact]
    public void NullAccount_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.Throws<ArgumentNullException>(() =>
            _factory.Create(null!, session, Now));
    }
}
