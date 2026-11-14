
using System.Text.Json;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Application.Modules.IdentitySocial.Queries;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityAcceptanceTests
{
    private static readonly DateTimeOffset RegisteredAtUtc =
        new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateUser()
    {
        return UserAccount.RegisterPending(
            "Test Traveler",
            "traveler@example.com",
            "test-only-placeholder-hash",
            RegisteredAtUtc);
    }

    // AT-REG-01
    [Fact]
    public void Registration_GeneratesValidUserId()
    {
        var user = CreateUser();

        Assert.True(Guid.TryParse(user.UserId, out var id));
        Assert.NotEqual(Guid.Empty, id);
    }

    // AT-REG-02
    [Fact]
    public void Registration_GeneratesUniqueUserIds()
    {
        var first = CreateUser();
        var second = CreateUser();

        Assert.NotEqual(first.UserId, second.UserId);
    }

    // AT-REG-03
    [Fact]
    public void Registration_StartsPendingVerification()
    {
        var user = CreateUser();

        Assert.Equal(
            UserAccountStatus.PendingVerification,
            user.Status);

        Assert.Null(user.VerifiedAtUtc);
    }

    // AT-REG-04
    [Fact]
    public void Registration_NormalizesUserDetails()
    {
        var user = UserAccount.RegisterPending(
            "  Wayhunter User  ",
            "  USER@EXAMPLE.COM  ",
            "test-only-placeholder-hash",
            RegisteredAtUtc);

        Assert.Equal("Wayhunter User", user.DisplayName);
        Assert.Equal("user@example.com", user.NormalizedEmail);
    }

    // AT-REG-05
    [Fact]
    public void Registration_RejectsEmptyDisplayName()
    {
        Assert.Throws<ArgumentException>(() =>
            UserAccount.RegisterPending(
                " ",
                "test@example.com",
                "test-only-placeholder-hash",
                RegisteredAtUtc));
    }

    // AT-REG-06
    [Fact]
    public void Registration_RejectsEmptyEmail()
    {
        Assert.Throws<ArgumentException>(() =>
            UserAccount.RegisterPending(
                "Traveler",
                " ",
                "test-only-placeholder-hash",
                RegisteredAtUtc));
    }

    // AT-REG-07
    [Fact]
    public void Registration_RejectsEmptyPasswordHash()
    {
        Assert.Throws<ArgumentException>(() =>
            UserAccount.RegisterPending(
                "Traveler",
                "test@example.com",
                " ",
                RegisteredAtUtc));
    }

    // AT-REG-08
    [Fact]
    public void Registration_RejectsNonUtcTimestamp()
    {
        var nonUtcTime = new DateTimeOffset(
            2026, 11, 14, 10, 0, 0,
            TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            UserAccount.RegisterPending(
                "Traveler",
                "test@example.com",
                "test-only-placeholder-hash",
                nonUtcTime));
    }

    // AT-REG-09
    [Fact]
    public void Verification_KeepsSameUserId()
    {
        var user = CreateUser();
        var originalUserId = user.UserId;

        user.ActivateAfterVerifiedOtp(
            RegisteredAtUtc.AddMinutes(2));

        Assert.Equal(originalUserId, user.UserId);
        Assert.Equal(UserAccountStatus.Active, user.Status);
    }

    // AT-REG-10
    [Fact]
    public void Verification_CannotBeAppliedTwice()
    {
        var user = CreateUser();

        user.ActivateAfterVerifiedOtp(
            RegisteredAtUtc.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() =>
            user.ActivateAfterVerifiedOtp(
                RegisteredAtUtc.AddMinutes(3)));
    }

    // AT-HANDOFF-01: Identity data for M2-M7
    [Fact]
    public void HandoffSnapshot_ContainsRequiredIdentityFields()
    {
        var user = CreateUser();
        var itemId = Guid.NewGuid();

        var snapshot = ApprovalIdentitySnapshotDto.Create(
            user,
            itemId,
            RegisteredAtUtc);

        Assert.Equal(user.UserId, snapshot.UserId);
        Assert.Equal(
            user.DisplayName,
            snapshot.DisplayNameSnapshot);

        Assert.Equal(itemId, snapshot.ItemId);
        Assert.Equal(
            RegisteredAtUtc,
            snapshot.ServerTimestampUtc);
    }

    // AT-HANDOFF-02
    [Fact]
    public void HandoffSnapshot_RejectsEmptyItemId()
    {
        var user = CreateUser();

        Assert.Throws<ArgumentException>(() =>
            ApprovalIdentitySnapshotDto.Create(
                user,
                Guid.Empty,
                RegisteredAtUtc));
    }

    // AT-PRIVACY-01
    [Fact]
    public void PublicProfile_DoesNotExposeSensitiveFields()
    {
        var user = CreateUser();

        var publicProfile =
            ProfileViewFactory.CreatePublic(user);

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(publicProfile));

        var root = json.RootElement;

        Assert.True(root.TryGetProperty("UserId", out _));

        Assert.False(root.TryGetProperty("Email", out _));
        Assert.False(root.TryGetProperty("PhoneNumber", out _));
        Assert.False(root.TryGetProperty("PasswordHash", out _));
        Assert.False(root.TryGetProperty("PrivateLatitude", out _));
        Assert.False(root.TryGetProperty("PrivateLongitude", out _));
    }

    // AT-PRIVACY-02
    [Fact]
    public void PrivateProfile_RejectsAnotherUser()
    {
        var user = CreateUser();

        Assert.Throws<UnauthorizedAccessException>(() =>
            ProfileViewFactory.CreateForOwner(
                user,
                Guid.NewGuid().ToString("D")));
    }
}
