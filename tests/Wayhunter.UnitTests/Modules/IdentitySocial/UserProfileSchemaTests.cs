
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class UserProfileSchemaTests
{
    private static readonly DateTimeOffset RegisteredAt =
        new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateAccount()
    {
        return UserAccount.RegisterPending(
            "Wayhunter Tester",
            "tester@example.com",
            "test-only-placeholder-hash",
            RegisteredAt);
    }

    [Fact]
    public void Profile_ShouldReuseAccountUserId()
    {
        var account = CreateAccount();

        var profile = UserProfile.CreateForUser(
            account,
            RegisteredAt);

        Assert.Equal(account.UserId, profile.UserId);
    }

    [Fact]
    public void NewProfile_ShouldStartWithEmptyPublicDetails()
    {
        var profile = UserProfile.CreateForUser(
            CreateAccount(),
            RegisteredAt);

        Assert.Null(profile.AvatarUrl);
        Assert.Null(profile.Bio);
        Assert.Null(profile.UpdatedAtUtc);
    }

    [Fact]
    public void Profile_ShouldStoreCreationTimestamp()
    {
        var profile = UserProfile.CreateForUser(
            CreateAccount(),
            RegisteredAt);

        Assert.Equal(RegisteredAt, profile.CreatedAtUtc);
    }

    [Fact]
    public void Profile_ShouldRejectNonUtcCreationTime()
    {
        var invalidTime = RegisteredAt.ToOffset(
            TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            UserProfile.CreateForUser(
                CreateAccount(),
                invalidTime));
    }

    [Fact]
    public void Profile_ShouldNotPredateAccount()
    {
        Assert.Throws<ArgumentException>(() =>
            UserProfile.CreateForUser(
                CreateAccount(),
                RegisteredAt.AddMinutes(-1)));
    }

    [Fact]
    public void PublicDetails_ShouldBeUpdated()
    {
        var profile = UserProfile.CreateForUser(
            CreateAccount(),
            RegisteredAt);

        var updateTime = RegisteredAt.AddMinutes(5);

        profile.UpdatePublicDetails(
            " https://example.com/avatar.jpg ",
            " Sri Lanka traveler ",
            updateTime);

        Assert.Equal(
            "https://example.com/avatar.jpg",
            profile.AvatarUrl);

        Assert.Equal(
            "Sri Lanka traveler",
            profile.Bio);

        Assert.Equal(updateTime, profile.UpdatedAtUtc);
    }

    [Fact]
    public void PublicDetails_ShouldAllowClearing()
    {
        var profile = UserProfile.CreateForUser(
            CreateAccount(),
            RegisteredAt);

        profile.UpdatePublicDetails(
            null,
            "Initial bio",
            RegisteredAt.AddMinutes(1));

        profile.UpdatePublicDetails(
            "",
            " ",
            RegisteredAt.AddMinutes(2));

        Assert.Null(profile.AvatarUrl);
        Assert.Null(profile.Bio);
    }

    [Fact]
    public void PublicDetails_ShouldRejectEarlierUpdate()
    {
        var profile = UserProfile.CreateForUser(
            CreateAccount(),
            RegisteredAt);

        profile.UpdatePublicDetails(
            null,
            "First update",
            RegisteredAt.AddMinutes(5));

        Assert.Throws<ArgumentException>(() =>
            profile.UpdatePublicDetails(
                null,
                "Older update",
                RegisteredAt.AddMinutes(2)));
    }
}
