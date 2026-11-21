using System;

namespace Wayhunter.Application.Modules.Games.Validators;

/// <summary>
/// Input validation for proposed M5 recovery leases.
/// Atomic ownership and expiry checks must be enforced in SQL.
/// </summary>
public static class GameProofSubmissionLeaseRules
{
    public static void ValidateClaim(string claimToken, DateTimeOffset nowUtc, DateTimeOffset leaseExpiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(claimToken) || claimToken.Length > 128)
            throw new ArgumentException("Invalid recovery claim token.", nameof(claimToken));

        if (leaseExpiresAtUtc <= nowUtc)
            throw new ArgumentOutOfRangeException(nameof(leaseExpiresAtUtc), "Lease expiry must be in the future.");
    }

    public static bool IsExpired(DateTimeOffset? leaseExpiresAtUtc, DateTimeOffset nowUtc)
        => leaseExpiresAtUtc.HasValue && leaseExpiresAtUtc.Value <= nowUtc;
}
