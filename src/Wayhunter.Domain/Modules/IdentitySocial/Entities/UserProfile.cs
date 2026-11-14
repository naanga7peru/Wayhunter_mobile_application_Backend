
namespace Wayhunter.Domain.Modules.IdentitySocial.Entities;

public sealed class UserProfile
{
    public string UserId { get; private set; } = string.Empty;

    public string? AvatarUrl { get; private set; }

    public string? Bio { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    // Reserved for future EF Core materialization.
    private UserProfile()
    {
    }

    private UserProfile(
        string userId,
        DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
    }

    public static UserProfile CreateForUser(
        UserAccount account,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(account);

        ValidateUtc(createdAtUtc);

        if (createdAtUtc < account.CreatedAtUtc)
        {
            throw new ArgumentException(
                "Profile cannot predate the user account.");
        }

        return new UserProfile(
            account.UserId,
            createdAtUtc);
    }

    public void UpdatePublicDetails(
        string? avatarUrl,
        string? bio,
        DateTimeOffset updatedAtUtc)
    {
        ValidateUtc(updatedAtUtc);

        if (updatedAtUtc < CreatedAtUtc ||
            (UpdatedAtUtc.HasValue &&
             updatedAtUtc < UpdatedAtUtc.Value))
        {
            throw new ArgumentException(
                "Invalid profile update timestamp.");
        }

        AvatarUrl = NormalizeOptional(avatarUrl);
        Bio = NormalizeOptional(bio);
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void ValidateUtc(DateTimeOffset value)
    {
        if (value == default ||
            value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A valid UTC timestamp is required.");
        }
    }
}
