
namespace Wayhunter.Domain.Modules.IdentitySocial.Entities;

public sealed class IdentityOtpChallenge
{
    public string ChallengeId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;

    // Store a secure verifier, never the plain OTP.
    public string OtpVerifierHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public int FailedAttemptCount { get; private set; }
    public int MaxFailedAttempts { get; private set; }

    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    private IdentityOtpChallenge()
    {
    }

    private IdentityOtpChallenge(
        string userId,
        string otpVerifierHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        int maxFailedAttempts)
    {
        ChallengeId = Guid.NewGuid().ToString("D");
        UserId = userId;
        OtpVerifierHash = otpVerifierHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        MaxFailedAttempts = maxFailedAttempts;
    }

    public static IdentityOtpChallenge Create(
        string userId,
        string otpVerifierHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        int maxFailedAttempts = 5)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.");

        if (string.IsNullOrWhiteSpace(otpVerifierHash))
            throw new ArgumentException("OTP verifier is required.");

        ValidateUtc(createdAtUtc);
        ValidateUtc(expiresAtUtc);

        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException(
                "OTP expiry must be after creation.");

        if (maxFailedAttempts <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxFailedAttempts));

        return new IdentityOtpChallenge(
            userId.Trim(),
            otpVerifierHash,
            createdAtUtc,
            expiresAtUtc,
            maxFailedAttempts);
    }

    public bool CanAttemptAt(DateTimeOffset nowUtc)
    {
        ValidateUtc(nowUtc);

        return nowUtc >= CreatedAtUtc
            && nowUtc < ExpiresAtUtc
            && FailedAttemptCount < MaxFailedAttempts
            && ConsumedAtUtc is null
            && RevokedAtUtc is null;
    }

    public void RecordFailedAttempt(DateTimeOffset attemptedAtUtc)
    {
        if (!CanAttemptAt(attemptedAtUtc))
            throw new InvalidOperationException(
                "OTP challenge is no longer usable.");

        FailedAttemptCount++;
    }

    // Call ONLY after a trusted service validates the OTP.
    public void ConsumeAfterExternalVerification(
        DateTimeOffset verifiedAtUtc)
    {
        if (!CanAttemptAt(verifiedAtUtc))
            throw new InvalidOperationException(
                "OTP challenge is no longer usable.");

        ConsumedAtUtc = verifiedAtUtc;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        ValidateUtc(revokedAtUtc);

        if (revokedAtUtc < CreatedAtUtc)
            throw new ArgumentException(
                "Revocation cannot predate challenge creation.");

        if (RevokedAtUtc.HasValue || ConsumedAtUtc.HasValue)
            throw new InvalidOperationException(
                "OTP challenge is already closed.");

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
