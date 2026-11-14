
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Application.Modules.IdentitySocial.Queries;

public static class ProfileViewFactory
{
    public static PublicProfileDto CreatePublic(
        UserAccount account,
        string? avatarUrl = null,
        string? bio = null)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new PublicProfileDto(
            account.UserId,
            account.DisplayName,
            avatarUrl,
            bio);
    }

    public static PrivateProfileDto CreateForOwner(
        UserAccount account,
        string authenticatedUserId,
        string? phoneNumber = null,
        string? avatarUrl = null,
        string? bio = null)
    {
        ArgumentNullException.ThrowIfNull(account);

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
                "You cannot view another user's private profile.");
        }

        return new PrivateProfileDto(
            account.UserId,
            account.DisplayName,
            avatarUrl,
            bio,
            account.NormalizedEmail,
            phoneNumber);
    }
}
