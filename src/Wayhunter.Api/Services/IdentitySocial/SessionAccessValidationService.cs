
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class SessionAccessValidationService
{
    public bool CanAccess(
        UserAccount? account,
        IdentitySession? session,
        string? authenticatedUserId,
        string? authenticatedSessionId,
        DateTimeOffset nowUtc)
    {
        if (nowUtc == default ||
            nowUtc.Offset != TimeSpan.Zero)
        {
            return false;
        }

        if (account is null || session is null)
            return false;

        if (account.Status != UserAccountStatus.Active ||
            !account.VerifiedAtUtc.HasValue ||
            account.DeletionRequestedAtUtc.HasValue)
        {
            return false;
        }

        if (!TryParseId(authenticatedUserId, out var userId) ||
            !TryParseId(account.UserId, out var accountId) ||
            !TryParseId(session.UserId, out var sessionUserId))
        {
            return false;
        }

        if (userId != accountId || userId != sessionUserId)
            return false;

        if (!TryParseId(authenticatedSessionId, out var sessionId) ||
            !TryParseId(session.SessionId, out var storedSessionId) ||
            sessionId != storedSessionId)
        {
            return false;
        }

        var verifiedAt = account.VerifiedAtUtc.Value;

        if (verifiedAt == default ||
            verifiedAt.Offset != TimeSpan.Zero ||
            account.CreatedAtUtc == default ||
            account.CreatedAtUtc.Offset != TimeSpan.Zero ||
            verifiedAt < account.CreatedAtUtc)
        {
            return false;
        }

        if (session.CreatedAtUtc < verifiedAt)
            return false;

        // A revoked session must never authorize access.
        if (session.RevokedAtUtc.HasValue)
            return false;

        return session.IsActiveAt(nowUtc);
    }

    private static bool TryParseId(
        string? value,
        out Guid parsed)
    {
        parsed = Guid.Empty;

        return !string.IsNullOrWhiteSpace(value) &&
            Guid.TryParseExact(
                value.Trim(), "D", out parsed) &&
            parsed != Guid.Empty;
    }
}
