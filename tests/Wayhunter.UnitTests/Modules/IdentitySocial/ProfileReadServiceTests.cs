
using System.Security.Claims;
using System.Text.Json;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class ProfileReadServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateUser()
    {
        return UserAccount.RegisterPending(
            "Wayhunter Traveler",
            "traveler@example.com",
            "test-only-placeholder-hash",
            Now);
    }

    private static ClaimsPrincipal AuthenticatedAs(
        string userId)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        userId)
                },
                "TestAuthentication"));
    }

    [Fact]
    public void PublicProfile_ShouldNotExposePrivateData()
    {
        var user = CreateUser();

        var result =
            ProfileReadService.GetPublicProfile(user);

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(result));

        Assert.Equal(user.UserId, result.UserId);
        Assert.False(json.RootElement.TryGetProperty(
            "Email", out _));
        Assert.False(json.RootElement.TryGetProperty(
            "PasswordHash", out _));
    }

    [Fact]
    public void AuthenticatedOwner_ShouldReadPrivateProfile()
    {
        var user = CreateUser();

        var result = ProfileReadService.GetOwnerProfile(
            user,
            AuthenticatedAs(user.UserId));

        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal(
            "traveler@example.com",
            result.Email);
    }

    [Fact]
    public void AnotherUser_ShouldBeDenied()
    {
        var user = CreateUser();

        Assert.Throws<UnauthorizedAccessException>(() =>
            ProfileReadService.GetOwnerProfile(
                user,
                AuthenticatedAs(
                    Guid.NewGuid().ToString("D"))));
    }

    [Fact]
    public void UnauthenticatedUser_ShouldBeDenied()
    {
        var user = CreateUser();

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        user.UserId)
                }));

        Assert.Throws<UnauthorizedAccessException>(() =>
            ProfileReadService.GetOwnerProfile(
                user,
                principal));
    }

    [Fact]
    public void MismatchedProfile_ShouldBeRejected()
    {
        var user = CreateUser();

        var anotherUser = UserAccount.RegisterPending(
            "Another Traveler",
            "another@example.com",
            "test-only-placeholder-hash",
            Now);

        var anotherProfile = UserProfile.CreateForUser(
            anotherUser,
            Now);

        Assert.Throws<InvalidOperationException>(() =>
            ProfileReadService.GetPublicProfile(
                user,
                anotherProfile));
    }

    [Fact]
    public void DisplayNameEdit_ShouldKeepSameUserId()
    {
        var user = CreateUser();
        var originalId = user.UserId;

        user.UpdateDisplayName("Updated Traveler");

        var result = ProfileReadService.GetOwnerProfile(
            user,
            AuthenticatedAs(originalId));

        Assert.Equal(originalId, result.UserId);
        Assert.Equal(
            "Updated Traveler",
            result.DisplayName);
    }
}
