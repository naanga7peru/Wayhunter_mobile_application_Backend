
using System.Security.Claims;

namespace Wayhunter.Api.Contracts;

public enum IdentityEndpointAccess
{
    Anonymous = 0,
    AuthenticatedUser = 1,
    Administrator = 2
}

public static class IdentityApiContract
{
    // Proposed routes, not yet active endpoints.
    public const string MobileBaseRoute =
        "api/mobile/identity";

    public const string AdminBaseRoute =
        "api/admin/identity";

    public const string RegisterRoute =
        MobileBaseRoute + "/register";

    public const string LoginRoute =
        MobileBaseRoute + "/login";

    public const string VerifyOtpRoute =
        MobileBaseRoute + "/verify-otp";

    public const string MyProfileRoute =
        MobileBaseRoute + "/me";

    public const string AdminUsersRoute =
        AdminBaseRoute + "/users";

    public const string CorrelationIdHeader =
        "X-Correlation-ID";

    public static IdentityEndpointAccess GetAccessRequirement(
        string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            throw new ArgumentException(
                "Route is required.",
                nameof(route));

        string path = route.Trim()
            .Trim('/')
            .ToLowerInvariant();

        return path switch
        {
            RegisterRoute => IdentityEndpointAccess.Anonymous,

            LoginRoute => IdentityEndpointAccess.Anonymous,

            VerifyOtpRoute => IdentityEndpointAccess.Anonymous,

            MyProfileRoute =>
                IdentityEndpointAccess.AuthenticatedUser,

            AdminUsersRoute =>
                IdentityEndpointAccess.Administrator,

            _ => throw new ArgumentException(
                "Unknown identity API route.",
                nameof(route))
        };
    }

    public static string? GetAuthenticatedUserId(
        ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Identity?.IsAuthenticated != true)
            return null;

        string? userId = user.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = user.FindFirst("sub")?.Value;
        }

        return string.IsNullOrWhiteSpace(userId)
            ? null
            : userId.Trim();
    }
}
