
using System;
using System.Security.Claims;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementAdminPrincipalReadinessTests
{
    private const string AdminRole = "Admin";

    private static readonly string ValidUserId =
        Guid.NewGuid().ToString("D");

    // Test 1
    [Fact]
    public void ValidAuthenticatedAdmin_ShouldBeAccepted()
    {
        var principal = CreatePrincipal(
            ValidUserId,
            AdminRole);

        var result =
            AdvertisementAdminPrincipalReadiness
                .HasAuthenticatedAdminRole(
                    principal,
                    AdminRole);

        Assert.True(result);
    }

    // Test 2
    [Fact]
    public void AnonymousUser_ShouldBeRejected()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    ValidUserId),

                new Claim(
                    ClaimTypes.Role,
                    AdminRole)
            });

        var principal = new ClaimsPrincipal(identity);

        Assert.False(
            AdvertisementAdminPrincipalReadiness
                .HasAuthenticatedAdminRole(
                    principal,
                    AdminRole));
    }

    // Test 3
    [Fact]
    public void NormalUser_ShouldBeRejected()
    {
        var principal = CreatePrincipal(
            ValidUserId,
            "Traveler");

        Assert.False(
            AdvertisementAdminPrincipalReadiness
                .HasAuthenticatedAdminRole(
                    principal,
                    AdminRole));
    }

    // Test 4
    [Fact]
    public void InvalidUserId_ShouldBeRejected()
    {
        var principal = CreatePrincipal(
            "invalid-user-id",
            AdminRole);

        Assert.False(
            AdvertisementAdminPrincipalReadiness
                .HasAuthenticatedAdminRole(
                    principal,
                    AdminRole));
    }

    // Test 5
    [Fact]
    public void DuplicateIdentityClaims_ShouldBeRejected()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    ValidUserId),

                new Claim(
                    ClaimTypes.NameIdentifier,
                    ValidUserId),

                new Claim(
                    ClaimTypes.Role,
                    AdminRole)
            },
            "TestAuthentication");

        var principal = new ClaimsPrincipal(identity);

        Assert.False(
            AdvertisementAdminPrincipalReadiness
                .HasAuthenticatedAdminRole(
                    principal,
                    AdminRole));
    }

    // Test 6
    [Fact]
    public void MissingAdminRoleConfiguration_ShouldBeRejected()
    {
        var principal = CreatePrincipal(
            ValidUserId,
            AdminRole);

        Assert.False(
            AdvertisementAdminPrincipalReadiness
                .HasAuthenticatedAdminRole(
                    principal,
                    null));

        Assert.False(
            AdvertisementAdminPrincipalReadiness
                .HasAuthenticatedAdminRole(
                    principal,
                    " "));
    }

    private static ClaimsPrincipal CreatePrincipal(
        string userId,
        string role)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId),

                new Claim(
                    ClaimTypes.Role,
                    role)
            },
            "TestAuthentication");

        return new ClaimsPrincipal(identity);
    }
}
