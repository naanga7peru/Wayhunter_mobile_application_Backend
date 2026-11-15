
using System;
using System.Security.Claims;
using Wayhunter.Api.Contracts;

namespace Wayhunter.Api.Contracts.HomeAdsAnalytics;

public static class AdvertisementAdminPrincipalReadiness
{
    // Checks an already-authenticated principal only.
    // This does not validate JWT tokens or authorize API endpoints.
    public static bool HasAuthenticatedAdminRole(
        ClaimsPrincipal? principal,
        string? administratorRole)
    {
        if (principal is null)
            return false;

        if (string.IsNullOrWhiteSpace(administratorRole))
            return false;

        // Reuse the M1 trusted identity claim contract.
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                principal,
                out _))
        {
            return false;
        }

        // The actual role name and claim mapping must
        // be supplied by the approved M1 configuration.
        return principal.IsInRole(administratorRole);
    }
}
