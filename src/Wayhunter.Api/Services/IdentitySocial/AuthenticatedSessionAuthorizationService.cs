
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed record AuthorizedSessionIdentity(
    string UserId,
    string SessionId);

public sealed class AuthenticatedSessionAuthorizationService
{
    private readonly SignedSessionTokenService _tokens;
    private readonly IAuthenticatedSessionReader _reader;
    private readonly TimeProvider _clock;

    public AuthenticatedSessionAuthorizationService(
        SignedSessionTokenService tokens,
        IAuthenticatedSessionReader reader,
        TimeProvider clock)
    {
        _tokens = tokens ??
            throw new ArgumentNullException(nameof(tokens));

        _reader = reader ??
            throw new ArgumentNullException(nameof(reader));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
    }

    public async Task<AuthorizedSessionIdentity?> AuthorizeAsync(
        string? bearerToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(bearerToken))
            return null;

        ClaimsPrincipal principal;

        try
        {
            // First verify JWT signature, expiry,
            // issuer, audience and required claims.
            principal = _tokens.Validate(bearerToken);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }

        var userId = principal.FindFirst("sub")?.Value;
        var sessionId = principal.FindFirst("sid")?.Value;

        if (!TryId(userId, out var parsedUserId) ||
            !TryId(sessionId, out var parsedSessionId))
        {
            return null;
        }

        userId = parsedUserId.ToString("D");
        sessionId = parsedSessionId.ToString("D");

        // These records must come from trusted,
        // server-side persistence.
        var account = await _reader.FindAccountAsync(
            userId, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (account is null ||
            account.Status != UserAccountStatus.Active ||
            !account.VerifiedAtUtc.HasValue ||
            account.DeletionRequestedAtUtc.HasValue)
        {
            return null;
        }

        var session = await _reader.FindSessionAsync(
            sessionId, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (session is null)
            return null;

        if (!TryId(account.UserId, out var storedUserId) ||
            storedUserId != parsedUserId ||
            !TryId(session.UserId, out var ownerId) ||
            ownerId != parsedUserId ||
            !TryId(session.SessionId, out var storedSessionId) ||
            storedSessionId != parsedSessionId)
        {
            return null;
        }

        var verifiedAt = account.VerifiedAtUtc.Value;

        if (account.CreatedAtUtc == default ||
            account.CreatedAtUtc.Offset != TimeSpan.Zero ||
            verifiedAt == default ||
            verifiedAt.Offset != TimeSpan.Zero ||
            verifiedAt < account.CreatedAtUtc ||
            session.CreatedAtUtc < verifiedAt)
        {
            return null;
        }

        // Revocation must be checked against persisted state
        // on every protected operation.
        if (session.RevokedAtUtc.HasValue ||
            !session.IsActiveAt(_clock.GetUtcNow()))
        {
            return null;
        }

        return new AuthorizedSessionIdentity(
            userId,
            sessionId);
    }

    private static bool TryId(
        string? value,
        out Guid parsed)
    {
        return Guid.TryParseExact(
            value, "D", out parsed) &&
            parsed != Guid.Empty;
    }
}
