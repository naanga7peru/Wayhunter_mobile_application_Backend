namespace Wayhunter.Domain.Modules.IdentitySocial.Entities;

public sealed class IdentityPasswordResetCredential
{
    public Guid CredentialId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    private IdentityPasswordResetCredential() { }

    private IdentityPasswordResetCredential(
        Guid credentialId,
        string userId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        CredentialId = credentialId;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static IdentityPasswordResetCredential Create(
        string userId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
            throw new ArgumentException("SHA-256 token hash is required.", nameof(tokenHash));

        ValidateUtc(createdAtUtc);
        ValidateUtc(expiresAtUtc);
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Reset credential expiry must follow creation.");

        return new IdentityPasswordResetCredential(
            Guid.NewGuid(), userId.Trim(), tokenHash, createdAtUtc, expiresAtUtc);
    }

    public bool CanUseAt(DateTimeOffset nowUtc)
    {
        ValidateUtc(nowUtc);
        return nowUtc >= CreatedAtUtc &&
               nowUtc < ExpiresAtUtc &&
               ConsumedAtUtc is null &&
               RevokedAtUtc is null;
    }

    public void ConsumeAt(DateTimeOffset consumedAtUtc)
    {
        if (!CanUseAt(consumedAtUtc))
            throw new InvalidOperationException("Reset credential is no longer usable.");
        ConsumedAtUtc = consumedAtUtc;
    }

    public void RevokeAt(DateTimeOffset revokedAtUtc)
    {
        ValidateUtc(revokedAtUtc);
        if (revokedAtUtc < CreatedAtUtc)
            throw new ArgumentException("Revocation cannot predate creation.");
        if (ConsumedAtUtc is not null || RevokedAtUtc is not null)
            throw new InvalidOperationException("Reset credential is already closed.");
        RevokedAtUtc = revokedAtUtc;
    }

    private static void ValidateUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Valid UTC timestamp is required.");
    }
}
