
using System.Security.Claims;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Application.Modules.IdentitySocial.Queries;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public static class ProfileReadService
{
    public static PublicProfileDto GetPublicProfile(
        UserAccount account,
        UserProfile? profile = null)
    {
        ValidateProfile(account, profile);

        return ProfileViewFactory.CreatePublic(
            account,
            avatarUrl: profile?.AvatarUrl,
            bio: profile?.Bio);
    }

    public static PrivateProfileDto GetOwnerProfile(
        UserAccount account,
        ClaimsPrincipal principal,
        UserProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(principal);

        // Identity must come from trusted authentication,
        // never from a client-supplied UserId.
        var authenticatedUserId =
            IdentityApiContract.GetAuthenticatedUserId(
                principal);

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        if (!string.Equals(
            account.UserId,
            authenticatedUserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Private profile access denied.");
        }

        ValidateProfile(account, profile);

        return ProfileViewFactory.CreateForOwner(
            account,
            authenticatedUserId,
            avatarUrl: profile?.AvatarUrl,
            bio: profile?.Bio);
    }

    private static void ValidateProfile(
        UserAccount account,
        UserProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (profile is not null &&
            !string.Equals(
                account.UserId,
                profile.UserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Profile belongs to another user.");
        }
    }
}
