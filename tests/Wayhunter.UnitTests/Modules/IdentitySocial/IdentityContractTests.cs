
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityContractTests
{
    private static readonly DateTimeOffset TestTime =
        new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateUser()
    {
        return UserAccount.RegisterPending(
            "Shayanthan",
            "shayanthan@example.com",
            "test-only-placeholder-hash",
            TestTime);
    }

    [Fact]
    public void NewUser_ShouldHaveUniqueUserId()
    {
        var user1 = CreateUser();
        var user2 = CreateUser();

        Assert.NotEqual(user1.UserId, user2.UserId);
        Assert.True(Guid.TryParse(user1.UserId, out _));
    }

    [Fact]
    public void NewUser_ShouldBePendingVerification()
    {
        var user = CreateUser();

        Assert.Equal(
            UserAccountStatus.PendingVerification,
            user.Status);

        Assert.Null(user.VerifiedAtUtc);
    }

    [Fact]
    public void VerifiedUser_ShouldBecomeActive()
    {
        var user = CreateUser();

        user.ActivateAfterVerifiedOtp(
            TestTime.AddMinutes(2));

        Assert.Equal(
            UserAccountStatus.Active,
            user.Status);

        Assert.NotNull(user.VerifiedAtUtc);
    }

    [Fact]
    public void ActiveUser_ShouldBeSuspended()
    {
        var user = CreateUser();

        user.ActivateAfterVerifiedOtp(
            TestTime.AddMinutes(2));

        user.Suspend();

        Assert.Equal(
            UserAccountStatus.Suspended,
            user.Status);
    }

    [Fact]
    public void ApprovalSnapshot_ShouldContainCorrectUserId()
    {
        var user = CreateUser();
        var itemId = Guid.NewGuid();

        var dto = ApprovalIdentitySnapshotDto.Create(
            user,
            itemId,
            TestTime);

        Assert.Equal(user.UserId, dto.UserId);
        Assert.Equal(
            user.DisplayName,
            dto.DisplayNameSnapshot);

        Assert.Equal(itemId, dto.ItemId);
        Assert.Equal(
            TestTime,
            dto.ServerTimestampUtc);
    }

    [Fact]
    public void ApprovalSnapshot_EmptyItemId_ShouldFail()
    {
        var user = CreateUser();

        Assert.Throws<ArgumentException>(() =>
            ApprovalIdentitySnapshotDto.Create(
                user,
                Guid.Empty,
                TestTime));
    }

    [Fact]
    public void InvalidUserName_ShouldFail()
    {
        Assert.Throws<ArgumentException>(() =>
            UserAccount.RegisterPending(
                "",
                "test@example.com",
                "test-only-placeholder-hash",
                TestTime));
    }
}
