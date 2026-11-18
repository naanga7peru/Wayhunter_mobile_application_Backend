using System;
using Wayhunter.Api.Contracts;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class TrustedIdentityDeletionGuardTests
{
    [Fact]
    public void ActiveAccountWithDeletionTimestamp_IsRejected()
    {
        var now = new DateTimeOffset(
            2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

        var account = UserAccount.RegisterPending(
            "Test Hunter",
            "deletion-guard@example.com",
            "test-password-hash",
            now);

        account.ActivateAfterVerifiedOtp(
            now.AddMinutes(5));

        // Simulate an inconsistent persisted account row.
        typeof(UserAccount)
            .GetProperty(nameof(UserAccount.DeletionRequestedAtUtc))!
            .SetValue(account, now.AddMinutes(10));

        Assert.Equal(UserAccountStatus.Active, account.Status);
        Assert.NotNull(account.DeletionRequestedAtUtc);

        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                account.UserId,
                account));
    }
}
