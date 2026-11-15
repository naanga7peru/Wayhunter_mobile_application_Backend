
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class VerifiedSessionCreationService
{
    public IdentitySession Create(
        UserAccount? account,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (account is null)
        {
            throw new InvalidOperationException(
                "A verified account is required.");
        }

        if (account.Status != UserAccountStatus.Active ||
            !account.VerifiedAtUtc.HasValue ||
            account.DeletionRequestedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Account is not eligible for a session.");
        }

        var verifiedAt = account.VerifiedAtUtc.Value;

        ValidateUtc(createdAtUtc);
        ValidateUtc(expiresAtUtc);
        ValidateUtc(account.CreatedAtUtc);
        ValidateUtc(verifiedAt);

        if (verifiedAt < account.CreatedAtUtc ||
            createdAtUtc < verifiedAt)
        {
            throw new InvalidOperationException(
                "Session cannot predate account verification.");
        }

        if (!Guid.TryParseExact(
                account.UserId, "D", out var userId) ||
            userId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Account identity is invalid.");
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException(
                "Session expiry must follow creation.");
        }

        return IdentitySession.Create(
            account.UserId,
            createdAtUtc,
            expiresAtUtc);
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
