
using Microsoft.IdentityModel.Tokens;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SignedSessionTokenServiceTests
{
    private const string Issuer = "wayhunter-test";
    private const string Audience = "wayhunter-mobile-test";

    // TEST ONLY. Never use this secret in production.
    private const string TestSecret =
        "test-only-32-byte-or-longer-jwt-signing-key-123";

    private static SignedSessionTokenService CreateService(
        string secret = TestSecret,
        string audience = Audience)
    {
        return new SignedSessionTokenService(
            Issuer, audience, secret);
    }

    private static (
        UserAccount Account,
        IdentitySession Session,
        DateTimeOffset Now) Setup()
    {
        var now = DateTimeOffset.UtcNow;

        var account = UserAccount.RegisterPending(
            "Test Traveler",
            "traveler@example.com",
            "test-password-hash",
            now.AddHours(-2));

        account.ActivateAfterVerifiedOtp(
            now.AddHours(-1));

        var session = IdentitySession.Create(
            account.UserId,
            now.AddMinutes(-5),
            now.AddMinutes(30));

        return (account, session, now);
    }

    [Fact]
    public void SignedToken_ValidatesSuccessfully()
    {
        var (account, session, now) = Setup();
        var service = CreateService();

        var token = service.Issue(account, session, now);
        var principal = service.Validate(token);

        Assert.Equal(
            account.UserId,
            principal.FindFirst("sub")?.Value);

        Assert.Equal(
            session.SessionId,
            principal.FindFirst("sid")?.Value);
    }

    [Fact]
    public void DifferentSigningKey_IsRejected()
    {
        var (account, session, now) = Setup();

        var token = CreateService().Issue(
            account, session, now);

        var other = CreateService(
            "another-test-only-32-byte-signing-key-56789");

        Assert.ThrowsAny<SecurityTokenException>(() =>
            other.Validate(token));
    }

    [Fact]
    public void DifferentAudience_IsRejected()
    {
        var (account, session, now) = Setup();

        var token = CreateService().Issue(
            account, session, now);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            CreateService(
                audience: "other-application").Validate(token));
    }

    [Fact]
    public void InvalidToken_IsRejected()
    {
        Assert.ThrowsAny<Exception>(() =>
            CreateService().Validate(
                "not.a.valid.jwt"));
    }

    [Fact]
    public void ShortSigningKey_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateService("short-key"));
    }

    [Fact]
    public void PendingAccount_CannotReceiveToken()
    {
        var (_, session, now) = Setup();

        var pending = UserAccount.RegisterPending(
            "Pending",
            "pending@example.com",
            "hash",
            now.AddHours(-2));

        Assert.Throws<InvalidOperationException>(() =>
            CreateService().Issue(pending, session, now));
    }

    [Fact]
    public void RevokedSession_CannotReceiveNewToken()
    {
        var (account, session, now) = Setup();
        session.Revoke(now);

        Assert.Throws<InvalidOperationException>(() =>
            CreateService().Issue(account, session, now));
    }
}
