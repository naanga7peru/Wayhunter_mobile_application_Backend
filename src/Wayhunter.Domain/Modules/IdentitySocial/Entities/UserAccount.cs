
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Domain.Modules.IdentitySocial.Entities;

public sealed class UserAccount
{
    public string UserId { get; private set; }
    public string DisplayName { get; private set; }
    public string NormalizedEmail { get; private set; }
    public string PasswordHash { get; private set; }

    public UserAccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    private UserAccount(
        string userId,
        string displayName,
        string normalizedEmail,
        string passwordHash,
        DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        DisplayName = displayName;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;

        Status = UserAccountStatus.PendingVerification;
    }

    public static UserAccount RegisterPending(
        string displayName,
        string email,
        string passwordHash,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException(
                "Display name is required.");

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "Email is required.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException(
                "Password hash is required.");

        if (createdAtUtc == default ||
            createdAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "A valid UTC creation time is required.");

        return new UserAccount(
            Guid.NewGuid().ToString("D"),
            displayName.Trim(),
            email.Trim().ToLowerInvariant(),
            passwordHash,
            createdAtUtc);
    }

    public void ActivateAfterVerifiedOtp(
        DateTimeOffset verifiedAtUtc)
    {
        if (Status != UserAccountStatus.PendingVerification)
            throw new InvalidOperationException(
                "Only pending accounts can be verified.");

        if (verifiedAtUtc.Offset != TimeSpan.Zero ||
            verifiedAtUtc < CreatedAtUtc)
            throw new ArgumentException(
                "Invalid verification time.");

        Status = UserAccountStatus.Active;
        VerifiedAtUtc = verifiedAtUtc;
    }

    public void Suspend()
    {
        if (Status != UserAccountStatus.Active)
            throw new InvalidOperationException(
                "Only active accounts can be suspended.");

        Status = UserAccountStatus.Suspended;
    }
}
