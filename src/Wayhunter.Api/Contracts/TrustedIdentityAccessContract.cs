
using System;
using System.Security.Claims;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Api.Contracts;

public static class TrustedIdentityAccessContract
{
    public static bool TryResolveUserId(
        ClaimsPrincipal principal,
        out string userId)
    {
        userId = string.Empty;

        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var nameId = principal.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        var subject = principal.FindFirst("sub")?.Value;

        // Conflicting identity claims must never be trusted.
        if (!string.IsNullOrWhiteSpace(nameId) &&
            !string.IsNullOrWhiteSpace(subject) &&
            !string.Equals(
                nameId.Trim(),
                subject.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var claimId = !string.IsNullOrWhiteSpace(nameId)
            ? nameId.Trim()
            : subject?.Trim();

        if (string.IsNullOrWhiteSpace(claimId) ||
            !Guid.TryParseExact(claimId, "D", out var parsed) ||
            parsed == Guid.Empty)
        {
            return false;
        }

        userId = parsed.ToString("D");
        return true;
    }

    public static bool IsActiveAccountOwner(
        string authenticatedUserId,
        UserAccount? account)
    {
        if (account is null ||
            account.Status != UserAccountStatus.Active ||
            !account.VerifiedAtUtc.HasValue)
        {
            return false;
        }

        if (!Guid.TryParseExact(
                authenticatedUserId, "D", out var claimId) ||
            claimId == Guid.Empty)
        {
            return false;
        }

        if (!Guid.TryParseExact(
                account.UserId, "D", out var accountId))
        {
            return false;
        }

        return claimId == accountId;
    }
}
