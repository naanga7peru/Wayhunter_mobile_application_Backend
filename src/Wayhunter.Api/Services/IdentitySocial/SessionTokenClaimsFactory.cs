
using System.Globalization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class SessionTokenClaimsFactory
{
    public IReadOnlyList<Claim> Create(
        UserAccount account,
        IdentitySession session,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(session);

        if (nowUtc == default ||
            nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC timestamp required.",
                nameof(nowUtc));
        }

        if (account.Status != UserAccountStatus.Active ||
            !account.VerifiedAtUtc.HasValue ||
            account.DeletionRequestedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Account is not eligible for token issuance.");
        }

        var verifiedAt = account.VerifiedAtUtc.Value;

        if (verifiedAt == default ||
            verifiedAt.Offset != TimeSpan.Zero ||
            account.CreatedAtUtc == default ||
            account.CreatedAtUtc.Offset != TimeSpan.Zero ||
            verifiedAt < account.CreatedAtUtc)
        {
            throw new InvalidOperationException(
                "Invalid account verification state.");
        }

        if (!TryValidGuid(account.UserId, out var userId) ||
            !TryValidGuid(session.UserId, out var ownerId) ||
            userId != ownerId ||
            !TryValidGuid(session.SessionId, out var sessionId))
        {
            throw new InvalidOperationException(
                "Invalid session ownership.");
        }

        if (session.CreatedAtUtc < verifiedAt ||
            session.RevokedAtUtc.HasValue ||
            !session.IsActiveAt(nowUtc))
        {
            throw new InvalidOperationException(
                "Session is not eligible for token issuance.");
        }

        var issuedAt = nowUtc.ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        var notBefore = Math.Max(
            nowUtc.ToUnixTimeSeconds(),
            session.CreatedAtUtc.ToUnixTimeSeconds())
            .ToString(CultureInfo.InvariantCulture);

        var expires = session.ExpiresAtUtc
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        if (long.Parse(expires, CultureInfo.InvariantCulture)
            <= long.Parse(notBefore, CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException(
                "Session expiry is too close for JWT issuance.");
        }

        return
        [
            new Claim(JwtRegisteredClaimNames.Sub,
                userId.ToString("D")),

            new Claim("sid",
                sessionId.ToString("D")),

            new Claim(JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString("D")),

            new Claim(JwtRegisteredClaimNames.Iat,
                issuedAt, ClaimValueTypes.Integer64),

            new Claim(JwtRegisteredClaimNames.Nbf,
                notBefore, ClaimValueTypes.Integer64),

            new Claim(JwtRegisteredClaimNames.Exp,
                expires, ClaimValueTypes.Integer64)
        ];
    }

    private static bool TryValidGuid(
        string? value,
        out Guid id)
    {
        id = Guid.Empty;

        return !string.IsNullOrWhiteSpace(value) &&
            Guid.TryParseExact(value, "D", out id) &&
            id != Guid.Empty;
    }
}
