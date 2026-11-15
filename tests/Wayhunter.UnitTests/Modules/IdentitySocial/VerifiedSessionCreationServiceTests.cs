
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class VerifiedSessionCreationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly VerifiedSessionCreationService _service = new();

    private static UserAccount CreateAccount(
        bool activate = true)
    {
        var account = UserAccount.RegisterPending(
            "Test Traveler",
            "traveler@example.com",
            "test-password-hash",
            Now.AddHours(-1));

        if (activate)
        {
            account.ActivateAfterVerifiedOtp(
                Now.AddMinutes(-50));
        }

        return account;
    }

    [Fact]
    public void ActiveVerifiedAccount_CreatesSession()
    {
        var account = CreateAccount();

        var session = _service.Create(
            account,
            Now,
            Now.AddHours(1));

        Assert.Equal(account.UserId, session.UserId);
        Assert.NotEqual(Guid.Empty.ToString("D"), session.SessionId);
        Assert.Equal(Now, session.CreatedAtUtc);
        Assert.Equal(Now.AddHours(1), session.ExpiresAtUtc);
        Assert.True(session.IsActiveAt(Now));
    }

    [Fact]
    public void NullAccount_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.Create(
                null,
                Now,
                Now.AddHours(1)));
    }

    [Fact]
    public void PendingAccount_IsRejected()
    {
        var account = CreateAccount(false);

        Assert.Throws<InvalidOperationException>(() =>
            _service.Create(
                account,
                Now,
                Now.AddHours(1)));
    }

    [Fact]
    public void SuspendedAccount_IsRejected()
    {
        var account = CreateAccount();
        account.Suspend();

        Assert.Throws<InvalidOperationException>(() =>
            _service.Create(
                account,
                Now,
                Now.AddHours(1)));
    }

    [Fact]
    public void DeletionRequestedAccount_IsRejected()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now);

        Assert.Throws<InvalidOperationException>(() =>
            _service.Create(
                account,
                Now,
                Now.AddHours(1)));
    }

    [Fact]
    public void SessionBeforeVerification_IsRejected()
    {
        var account = CreateAccount();

        Assert.Throws<InvalidOperationException>(() =>
            _service.Create(
                account,
                Now.AddMinutes(-55),
                Now.AddHours(1)));
    }

    [Fact]
    public void ExpiryBeforeCreation_IsRejected()
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(() =>
            _service.Create(
                account,
                Now,
                Now.AddMinutes(-1)));
    }

    [Fact]
    public void EqualExpiry_IsRejected()
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(() =>
            _service.Create(
                account,
                Now,
                Now));
    }

    [Fact]
    public void NonUtcCreation_IsRejected()
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(() =>
            _service.Create(
                account,
                Now.ToOffset(TimeSpan.FromHours(5.5)),
                Now.AddHours(1)));
    }

    [Fact]
    public void NonUtcExpiry_IsRejected()
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(() =>
            _service.Create(
                account,
                Now,
                Now.AddHours(1).ToOffset(
                    TimeSpan.FromHours(5.5))));
    }

    [Fact]
    public void CreatedAtVerificationTime_IsAllowed()
    {
        var account = CreateAccount();

        var verifiedAt = account.VerifiedAtUtc!.Value;

        var session = _service.Create(
            account,
            verifiedAt,
            verifiedAt.AddMinutes(30));

        Assert.Equal(verifiedAt, session.CreatedAtUtc);
    }

    [Fact]
    public void EveryNewSession_HasUniqueSessionId()
    {
        var account = CreateAccount();

        var first = _service.Create(
            account, Now, Now.AddHours(1));

        var second = _service.Create(
            account, Now, Now.AddHours(1));

        Assert.NotEqual(first.SessionId, second.SessionId);
    }
}
