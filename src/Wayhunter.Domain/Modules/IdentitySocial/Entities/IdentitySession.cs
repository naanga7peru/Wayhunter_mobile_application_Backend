
namespace Wayhunter.Domain.Modules.IdentitySocial.Entities;

public sealed class IdentitySession
{
    public string SessionId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    private IdentitySession()
    {
    }

    private IdentitySession(
        string sessionId,
        string userId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        SessionId = sessionId;
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static IdentitySession Create(
        string userId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.");

        ValidateUtc(createdAtUtc);
        ValidateUtc(expiresAtUtc);

        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException(
                "Session expiry must be after creation.");

        return new IdentitySession(
            Guid.NewGuid().ToString("D"),
            userId.Trim(),
            createdAtUtc,
            expiresAtUtc);
    }

    public bool IsActiveAt(DateTimeOffset nowUtc)
    {
        ValidateUtc(nowUtc);

        return nowUtc >= CreatedAtUtc &&
               nowUtc < ExpiresAtUtc &&
               (!RevokedAtUtc.HasValue ||
                nowUtc < RevokedAtUtc.Value);
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        ValidateUtc(revokedAtUtc);

        if (revokedAtUtc < CreatedAtUtc)
            throw new ArgumentException(
                "Revocation cannot predate session creation.");

        if (RevokedAtUtc.HasValue)
            throw new InvalidOperationException(
                "Session is already revoked.");

        RevokedAtUtc = revokedAtUtc;
    }

    private static void ValidateUtc(DateTimeOffset value)
    {
        if (value == default ||
            value.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "Valid UTC timestamp is required.");
    }
}
