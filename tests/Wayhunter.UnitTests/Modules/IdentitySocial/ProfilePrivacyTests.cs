
using System.Text.Json;
using Wayhunter.Application.Modules.IdentitySocial.Queries;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class ProfilePrivacyTests
{
    private static UserAccount CreateUser()
    {
        return UserAccount.RegisterPending(
            "Shayanthan",
            "test@example.com",
            "test-only-placeholder-hash",
            new DateTimeOffset(
                2026, 11, 14, 0, 0, 0,
                TimeSpan.Zero));
    }

    [Fact]
    public void PublicProfile_ShouldContainSafeFields()
    {
        var user = CreateUser();

        var profile = ProfileViewFactory.CreatePublic(
            user,
            avatarUrl: "https://example.com/avatar.jpg",
            bio: "Traveler");

        Assert.Equal(user.UserId, profile.UserId);
        Assert.Equal(user.DisplayName, profile.DisplayName);
        Assert.Equal("Traveler", profile.Bio);
    }

    [Fact]
    public void PublicProfile_ShouldNotExposePrivateFields()
    {
        var user = CreateUser();

        var profile = ProfileViewFactory.CreatePublic(user);

        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(profile));

        var root = document.RootElement;

        Assert.False(root.TryGetProperty("Email", out _));
        Assert.False(root.TryGetProperty("PhoneNumber", out _));
        Assert.False(root.TryGetProperty("PasswordHash", out _));
        Assert.False(root.TryGetProperty("PrivateLatitude", out _));
        Assert.False(root.TryGetProperty("PrivateLongitude", out _));
    }

    [Fact]
    public void OwnerProfile_ShouldContainEmailAndPhone()
    {
        var user = CreateUser();

        var profile = ProfileViewFactory.CreateForOwner(
            user,
            user.UserId,
            phoneNumber: "+94770000000");

        Assert.Equal(user.UserId, profile.UserId);
        Assert.Equal("test@example.com", profile.Email);
        Assert.Equal("+94770000000", profile.PhoneNumber);
    }

    [Fact]
    public void OwnerProfile_ShouldNotExposePasswordOrGps()
    {
        var user = CreateUser();

        var profile = ProfileViewFactory.CreateForOwner(
            user,
            user.UserId);

        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(profile));

        var root = document.RootElement;

        Assert.False(root.TryGetProperty("PasswordHash", out _));
        Assert.False(root.TryGetProperty("PrivateLatitude", out _));
        Assert.False(root.TryGetProperty("PrivateLongitude", out _));
    }

    [Fact]
    public void OtherUser_CannotAccessPrivateProfile()
    {
        var user = CreateUser();

        Assert.Throws<UnauthorizedAccessException>(() =>
            ProfileViewFactory.CreateForOwner(
                user,
                Guid.NewGuid().ToString("D")));
    }

    [Fact]
    public void UnauthenticatedUser_CannotAccessPrivateProfile()
    {
        var user = CreateUser();

        Assert.Throws<UnauthorizedAccessException>(() =>
            ProfileViewFactory.CreateForOwner(
                user,
                ""));
    }
}
