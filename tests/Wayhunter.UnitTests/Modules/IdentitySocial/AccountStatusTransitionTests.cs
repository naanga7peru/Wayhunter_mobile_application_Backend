
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class AccountStatusTransitionTests
{
    private static readonly DateTimeOffset TestTime =
        new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);

    private static UserAccount CreatePendingUser()
    {
        return UserAccount.RegisterPending(
            "Test User",
            "test@example.com",
            "test-only-hash",
            TestTime);
    }

    private static UserAccount CreateActiveUser()
    {
        var user = CreatePendingUser();

        user.ActivateAfterVerifiedOtp(
            TestTime.AddMinutes(1));

        return user;
    }

    [Fact]
    public void PendingUser_CannotBeSuspended()
    {
        var user = CreatePendingUser();

        Assert.Throws<InvalidOperationException>(
            () => user.Suspend());
    }

    [Fact]
    public void ActiveUser_CanBeSuspended()
    {
        var user = CreateActiveUser();

        user.Suspend();

        Assert.Equal(
            UserAccountStatus.Suspended,
            user.Status);
    }

    [Fact]
    public void SuspendedUser_CanBeReactivated()
    {
        var user = CreateActiveUser();

        user.Suspend();
        user.Reactivate();

        Assert.Equal(
            UserAccountStatus.Active,
            user.Status);
    }

    [Fact]
    public void ActiveUser_CannotBeReactivatedAgain()
    {
        var user = CreateActiveUser();

        Assert.Throws<InvalidOperationException>(
            () => user.Reactivate());
    }

    [Fact]
    public void ActiveUser_CanRequestDeletion()
    {
        var user = CreateActiveUser();
        var deletionTime = TestTime.AddDays(1);

        user.RequestDeletion(deletionTime);

        Assert.Equal(
            UserAccountStatus.DeletionRequested,
            user.Status);

        Assert.Equal(
            deletionTime,
            user.DeletionRequestedAtUtc);
    }

    [Fact]
    public void SuspendedUser_CanRequestDeletion()
    {
        var user = CreateActiveUser();

        user.Suspend();

        user.RequestDeletion(TestTime.AddDays(1));

        Assert.Equal(
            UserAccountStatus.DeletionRequested,
            user.Status);
    }

    [Fact]
    public void PendingUser_CannotRequestDeletionUsingThisFlow()
    {
        var user = CreatePendingUser();

        Assert.Throws<InvalidOperationException>(() =>
            user.RequestDeletion(TestTime.AddDays(1)));
    }

    [Fact]
    public void DeletedRequestUser_CannotBeReactivated()
    {
        var user = CreateActiveUser();

        user.RequestDeletion(TestTime.AddDays(1));

        Assert.Throws<InvalidOperationException>(
            () => user.Reactivate());
    }

    [Fact]
    public void DeletionRequest_CannotBeRepeated()
    {
        var user = CreateActiveUser();

        user.RequestDeletion(TestTime.AddDays(1));

        Assert.Throws<InvalidOperationException>(() =>
            user.RequestDeletion(TestTime.AddDays(2)));
    }

    [Fact]
    public void DeletionRequest_RequiresUtcTime()
    {
        var user = CreateActiveUser();

        var invalidTime = new DateTimeOffset(
            2026, 11, 15, 10, 0, 0,
            TimeSpan.FromHours(5));

        Assert.Throws<ArgumentException>(() =>
            user.RequestDeletion(invalidTime));
    }
}
