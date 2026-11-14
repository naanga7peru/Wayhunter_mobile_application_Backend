
namespace Wayhunter.Domain.Modules.IdentitySocial.Entities;

public sealed class IdentityConsent
{
    public string ConsentId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string ConsentKey { get; private set; } = string.Empty;

    public DateTimeOffset GrantedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    private IdentityConsent()
    {
    }

    private IdentityConsent(
        string consentId,
        string userId,
        string consentKey,
        DateTimeOffset grantedAtUtc)
    {
        ConsentId = consentId;
        UserId = userId;
        ConsentKey = consentKey;
        GrantedAtUtc = grantedAtUtc;
    }

    public static IdentityConsent Grant(
        string userId,
        string consentKey,
        DateTimeOffset grantedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException(
                "UserId is required.");

        if (string.IsNullOrWhiteSpace(consentKey))
            throw new ArgumentException(
                "Consent key is required.");

        ValidateUtc(grantedAtUtc);

        return new IdentityConsent(
            Guid.NewGuid().ToString("D"),
            userId.Trim(),
            consentKey.Trim().ToLowerInvariant(),
            grantedAtUtc);
    }

    public bool IsActiveAt(DateTimeOffset nowUtc)
    {
        ValidateUtc(nowUtc);

        return nowUtc >= GrantedAtUtc &&
               (!RevokedAtUtc.HasValue ||
                nowUtc < RevokedAtUtc.Value);
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        ValidateUtc(revokedAtUtc);

        if (revokedAtUtc < GrantedAtUtc)
            throw new ArgumentException(
                "Revocation cannot predate consent grant.");

        if (RevokedAtUtc.HasValue)
            throw new InvalidOperationException(
                "Consent has already been revoked.");

        RevokedAtUtc = revokedAtUtc;
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
