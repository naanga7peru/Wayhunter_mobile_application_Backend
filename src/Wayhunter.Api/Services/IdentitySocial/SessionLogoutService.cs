
namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class SessionLogoutService
{
    private readonly IIdentitySessionRevocationStore _store;

    public SessionLogoutService(
        IIdentitySessionRevocationStore store)
    {
        _store = store ??
            throw new ArgumentNullException(nameof(store));
    }

    public async Task<bool> LogoutAsync(
        string authenticatedUserId,
        string authenticatedSessionId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var userId = ParseId(
            authenticatedUserId,
            nameof(authenticatedUserId));

        var sessionId = ParseId(
            authenticatedSessionId,
            nameof(authenticatedSessionId));

        if (nowUtc == default ||
            nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A valid UTC timestamp is required.",
                nameof(nowUtc));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var session = await _store.FindAsync(
            sessionId,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Fail closed if the session does not exist.
        if (session is null)
            return false;

        // Do not revoke a session owned by another user.
        if (!Guid.TryParseExact(
                session.UserId, "D", out var ownerId) ||
            ownerId == Guid.Empty ||
            !string.Equals(
                ownerId.ToString("D"),
                userId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!Guid.TryParseExact(
                session.SessionId, "D", out var storedId) ||
            storedId == Guid.Empty ||
            !string.Equals(
                storedId.ToString("D"),
                sessionId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (session.CreatedAtUtc > nowUtc)
            return false;

        // The store must atomically enforce ownership
        // and persist revocation. A read alone is not enough.
        var result = await _store.TryRevokeAsync(
            userId,
            sessionId,
            nowUtc,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return result switch
        {
            SessionRevocationWriteResult.Revoked => true,
            SessionRevocationWriteResult.AlreadyRevoked => true,
            SessionRevocationWriteResult.NotFound => false,
            _ => throw new InvalidOperationException(
                "Unknown session revocation result.")
        };
    }

    private static string ParseId(
        string value,
        string parameterName)
    {
        if (!Guid.TryParseExact(
                value, "D", out var parsed) ||
            parsed == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid identity is required.",
                parameterName);
        }

        return parsed.ToString("D");
    }
}
