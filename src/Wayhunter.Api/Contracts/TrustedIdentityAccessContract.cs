
using System;
using System.Linq;
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
        ArgumentNullException.ThrowIfNull(principal);

        userId = string.Empty;

        if (principal.Identity?.IsAuthenticated != true)
            return false;

        var nameIds = principal
            .FindAll(ClaimTypes.NameIdentifier)
            .ToArray();

        var subjects = principal
            .FindAll("sub")
            .ToArray();

        // Never accept duplicate identity claims.
        if (nameIds.Length > 1 || subjects.Length > 1)
            return false;

        if (nameIds.Length == 0 && subjects.Length == 0)
            return false;

        Guid? nameId = null;
        Guid? subjectId = null;

        if (nameIds.Length == 1)
        {
            if (!TryParseUserId(nameIds[0].Value, out var id))
                return false;

            nameId = id;
        }

        if (subjects.Length == 1)
        {
            if (!TryParseUserId(subjects[0].Value, out var id))
                return false;

            subjectId = id;
        }

        if (nameId.HasValue && subjectId.HasValue &&
            nameId.Value != subjectId.Value)
        {
            return false;
        }

        var resolvedId = nameId ?? subjectId;

        if (!resolvedId.HasValue)
            return false;

        userId = resolvedId.Value.ToString("D");
        return true;
    }

    public static bool IsActiveAccountOwner(
        string authenticatedUserId,
        UserAccount? account)
    {
        if (account is null)
            return false;

        if (!TryParseUserId(
                authenticatedUserId, out var claimId))
        {
            return false;
        }

        if (!TryParseUserId(
                account.UserId, out var accountId))
        {
            return false;
        }

        if (claimId != accountId)
            return false;

        if (account.Status != UserAccountStatus.Active)
            return false;

        if (!account.VerifiedAtUtc.HasValue)
            return false;

        var verifiedAt = account.VerifiedAtUtc.Value;

        if (verifiedAt == default ||
            verifiedAt.Offset != TimeSpan.Zero)
        {
            return false;
        }

        if (account.CreatedAtUtc == default ||
            account.CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            return false;
        }

        if (verifiedAt < account.CreatedAtUtc)
            return false;

        return true;
    }

    private static bool TryParseUserId(
        string? value,
        out Guid userId)
    {
        userId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Guid.TryParseExact(
                value.Trim(), "D", out var parsed))
        {
            return false;
        }

        if (parsed == Guid.Empty)
            return false;

        userId = parsed;
        return true;
    }
}
