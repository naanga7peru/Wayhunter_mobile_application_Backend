
using System.Security.Claims;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityApiContractTests
{
    [Theory]
    [InlineData(
        IdentityApiContract.RegisterRoute,
        IdentityEndpointAccess.Anonymous)]
    [InlineData(
        IdentityApiContract.LoginRoute,
        IdentityEndpointAccess.Anonymous)]
    [InlineData(
        IdentityApiContract.VerifyOtpRoute,
        IdentityEndpointAccess.Anonymous)]
    [InlineData(
        IdentityApiContract.MyProfileRoute,
        IdentityEndpointAccess.AuthenticatedUser)]
    [InlineData(
        IdentityApiContract.AdminUsersRoute,
        IdentityEndpointAccess.Administrator)]
    public void Route_ShouldHaveCorrectAccessRequirement(
        string route,
        IdentityEndpointAccess expected)
    {
        var result =
            IdentityApiContract.GetAccessRequirement(route);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void UnknownRoute_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            IdentityApiContract.GetAccessRequirement(
                "api/mobile/identity/unknown"));
    }

    [Fact]
    public void AuthenticatedUser_ShouldReturnUserId()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "user-123")
            },
            "TestAuthentication");

        var principal = new ClaimsPrincipal(identity);

        var userId =
            IdentityApiContract.GetAuthenticatedUserId(
                principal);

        Assert.Equal("user-123", userId);
    }

    [Fact]
    public void AuthenticatedSubClaim_ShouldReturnUserId()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", "user-456")
            },
            "TestAuthentication");

        var principal = new ClaimsPrincipal(identity);

        var userId =
            IdentityApiContract.GetAuthenticatedUserId(
                principal);

        Assert.Equal("user-456", userId);
    }

    [Fact]
    public void UnauthenticatedUser_ShouldNotReturnUserId()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    "fake-user")
            });

        var principal = new ClaimsPrincipal(identity);

        var userId =
            IdentityApiContract.GetAuthenticatedUserId(
                principal);

        Assert.Null(userId);
    }

    [Fact]
    public void MissingUserIdClaim_ShouldReturnNull()
    {
        var identity = new ClaimsIdentity(
            Array.Empty<Claim>(),
            "TestAuthentication");

        var principal = new ClaimsPrincipal(identity);

        Assert.Null(
            IdentityApiContract.GetAuthenticatedUserId(
                principal));
    }

    [Fact]
    public void ApiError_ShouldContainStandardFields()
    {
        var result = ApiErrorResponse.Create(
            "authentication_required",
            "Authentication is required.",
            "request-123");

        Assert.Equal(
            "authentication_required",
            result.Code);

        Assert.Equal(
            "Authentication is required.",
            result.Message);

        Assert.Equal(
            "request-123",
            result.CorrelationId);
    }

    [Fact]
    public void ApiError_ShouldRejectEmptyCorrelationId()
    {
        Assert.Throws<ArgumentException>(() =>
            ApiErrorResponse.Create(
                "invalid_request",
                "Invalid request.",
                ""));
    }

    [Fact]
    public void ApiError_ShouldRejectEmptyCode()
    {
        Assert.Throws<ArgumentException>(() =>
            ApiErrorResponse.Create(
                "",
                "Invalid request.",
                "request-123"));
    }
}
