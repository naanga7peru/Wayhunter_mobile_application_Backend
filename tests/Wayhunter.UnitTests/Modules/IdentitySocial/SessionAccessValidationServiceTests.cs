
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SessionAccessValidationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly SessionAccessValidationService _service = new();

    private static UserAccount ActiveAccount()
    {
        var account = UserAccount.RegisterPending(
            "Test Traveler",
            "traveler@example.com",
            "test-password-hash",
            Now.AddHours(-1));

        account.ActivateAfterVerifiedOtp(
            Now.AddMinutes(-50));

        return account;
    }

    private static IdentitySession Session(UserAccount account)
    {
        return IdentitySession.Create(
            account.UserId,
            Now.AddMinutes(-20),
            Now.AddHours(1));
    }

    private bool CanAccess(
        UserAccount account,
        IdentitySession session,
        DateTimeOffset? at = null)
    {
        return _service.CanAccess(
            account,
            session,
            account.UserId,
            session.SessionId,
            at ?? Now);
    }

    [Fact]
    public void ActiveVerifiedUser_ValidSession_IsAllowed()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.True(CanAccess(account, session));
    }

    [Fact]
    public void ExpiredSession_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.False(
            CanAccess(account, session, Now.AddHours(1)));
    }

    [Fact]
    public void RevokedSession_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        session.Revoke(Now.AddMinutes(-1));

        Assert.False(CanAccess(account, session));
    }

    [Fact]
    public void PendingAccount_IsRejected()
    {
        var account = UserAccount.RegisterPending(
            "Pending User",
            "pending@example.com",
            "test-password-hash",
            Now.AddHours(-1));

        var session = Session(account);

        Assert.False(CanAccess(account, session));
    }

    [Fact]
    public void SuspendedAccount_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        account.Suspend();

        Assert.False(CanAccess(account, session));
    }

    [Fact]
    public void DeletionRequestedAccount_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        account.RequestDeletion(Now);

        Assert.False(CanAccess(account, session));
    }

    [Fact]
    public void DifferentUser_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.False(_service.CanAccess(
            account,
            session,
            Guid.NewGuid().ToString("D"),
            session.SessionId,
            Now));
    }

    [Fact]
    public void DifferentSessionId_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.False(_service.CanAccess(
            account,
            session,
            account.UserId,
            Guid.NewGuid().ToString("D"),
            Now));
    }

    [Fact]
    public void MissingAccount_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.False(_service.CanAccess(
            null,
            session,
            account.UserId,
            session.SessionId,
            Now));
    }

    [Fact]
    public void SessionCreatedBeforeVerification_IsRejected()
    {
        var account = ActiveAccount();

        var session = IdentitySession.Create(
            account.UserId,
            Now.AddMinutes(-55),
            Now.AddHours(1));

        Assert.False(CanAccess(account, session));
    }

    [Fact]
    public void FutureSession_IsRejected()
    {
        var account = ActiveAccount();

        var session = IdentitySession.Create(
            account.UserId,
            Now.AddMinutes(10),
            Now.AddHours(1));

        Assert.False(CanAccess(account, session));
    }

    [Fact]
    public void InvalidUtcTime_IsRejected()
    {
        var account = ActiveAccount();
        var session = Session(account);

        Assert.False(_service.CanAccess(
            account,
            session,
            account.UserId,
            session.SessionId,
            Now.ToOffset(TimeSpan.FromHours(5.5))));
    }
}
