
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class ServerGeneratedUserIdTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateUser()
    {
        return UserAccount.RegisterPending(
            "Original Traveler",
            "traveler@example.com",
            "test-only-placeholder-hash",
            Now);
    }

    [Fact]
    public void Registration_ShouldGenerateValidUserId()
    {
        var user = CreateUser();

        Assert.True(Guid.TryParse(user.UserId, out var id));
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void DifferentRegistrations_ShouldHaveDifferentUserIds()
    {
        var first = CreateUser();
        var second = CreateUser();

        Assert.NotEqual(first.UserId, second.UserId);
    }

    [Fact]
    public void DisplayNameEdit_ShouldPreserveUserId()
    {
        var user = CreateUser();
        var originalId = user.UserId;

        user.UpdateDisplayName("  New Traveler Name  ");

        Assert.Equal(originalId, user.UserId);
        Assert.Equal("New Traveler Name", user.DisplayName);
    }

    [Fact]
    public void InvalidDisplayName_ShouldBeRejected()
    {
        var user = CreateUser();
        var originalId = user.UserId;

        Assert.Throws<ArgumentException>(() =>
            user.UpdateDisplayName("   "));

        Assert.Throws<ArgumentException>(() =>
            user.UpdateDisplayName(new string('A', 201)));

        Assert.Equal(originalId, user.UserId);
        Assert.Equal("Original Traveler", user.DisplayName);
    }
}
