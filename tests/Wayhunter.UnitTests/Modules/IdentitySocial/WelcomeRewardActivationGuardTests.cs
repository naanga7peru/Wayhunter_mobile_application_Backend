
using System;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class WelcomeRewardActivationGuardTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static UserAccount CreatePendingAccount()
    {
        return UserAccount.RegisterPending(
            "Wayhunter Traveler",
            "traveler@example.com",
            "test-only-password-hash",
            CreatedAt);
    }

    [Fact]
    public void FirstOtpVerification_ActivatesAccount()
    {
        var account = CreatePendingAccount();
        var verifiedAt = CreatedAt.AddMinutes(5);

        account.ActivateAfterVerifiedOtp(verifiedAt);

        Assert.Equal(UserAccountStatus.Active, account.Status);
        Assert.Equal(verifiedAt, account.VerifiedAtUtc);
    }

    [Fact]
    public void RepeatedOtpVerification_CannotActivateTwice()
    {
        var account = CreatePendingAccount();
        var firstVerification = CreatedAt.AddMinutes(5);

        account.ActivateAfterVerifiedOtp(firstVerification);

        Assert.Throws<InvalidOperationException>(() =>
            account.ActivateAfterVerifiedOtp(
                firstVerification.AddMinutes(1)));

        Assert.Equal(
            firstVerification,
            account.VerifiedAtUtc);
    }

    [Fact]
    public void ReactivatedAccount_CannotRepeatOtpActivation()
    {
        var account = CreatePendingAccount();

        account.ActivateAfterVerifiedOtp(
            CreatedAt.AddMinutes(5));

        account.Suspend();
        account.Reactivate();

        Assert.Throws<InvalidOperationException>(() =>
            account.ActivateAfterVerifiedOtp(
                CreatedAt.AddMinutes(10)));

        Assert.Equal(UserAccountStatus.Active, account.Status);
    }

    [Fact]
    public void Verification_DoesNotChangeUserId()
    {
        var account = CreatePendingAccount();
        var originalUserId = account.UserId;

        account.ActivateAfterVerifiedOtp(
            CreatedAt.AddMinutes(5));

        Assert.Equal(originalUserId, account.UserId);
        Assert.True(Guid.TryParse(account.UserId, out _));
    }
}
