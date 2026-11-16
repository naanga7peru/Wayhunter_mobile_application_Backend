
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class SqlIdentitySessionRevocationStore
    : IIdentitySessionRevocationStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlIdentitySessionRevocationStore(
        WayhunterSharedDbContext db)
    {
        _db = db ??
            throw new ArgumentNullException(nameof(db));
    }

    public async Task<IdentitySession?> FindAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var id = NormalizeId(sessionId);
        if (id is null)
            return null;

        return await _db.IdentitySessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                session => session.SessionId == id,
                cancellationToken);
    }

    public async Task<SessionRevocationWriteResult> TryRevokeAsync(
        string userId,
        string sessionId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedUserId = NormalizeId(userId);
        var normalizedSessionId = NormalizeId(sessionId);

        if (normalizedUserId is null ||
            normalizedSessionId is null)
        {
            return SessionRevocationWriteResult.NotFound;
        }

        if (revokedAtUtc == default ||
            revokedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC revocation time is required.",
                nameof(revokedAtUtc));
        }

        // Atomic conditional SQL UPDATE:
        // SessionId + owner must match.
        // Only an unrevoked session can be changed.
        var updated = await _db.IdentitySessions
            .Where(session =>
                session.SessionId == normalizedSessionId &&
                session.UserId == normalizedUserId &&
                session.RevokedAtUtc == null &&
                session.CreatedAtUtc <= revokedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.RevokedAtUtc,
                    (DateTimeOffset?)revokedAtUtc),
                cancellationToken);

        if (updated == 1)
        {
            return SessionRevocationWriteResult.Revoked;
        }

        if (updated != 0)
        {
            throw new InvalidOperationException(
                "Unexpected session revocation update count.");
        }

        // No row updated. Distinguish an already-revoked
        // owned session from missing/unauthorized sessions.
        var existing = await _db.IdentitySessions
            .AsNoTracking()
            .Where(session =>
                session.SessionId == normalizedSessionId &&
                session.UserId == normalizedUserId)
            .Select(session => new
            {
                session.CreatedAtUtc,
                session.RevokedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null ||
            existing.CreatedAtUtc > revokedAtUtc)
        {
            return SessionRevocationWriteResult.NotFound;
        }

        return existing.RevokedAtUtc.HasValue
            ? SessionRevocationWriteResult.AlreadyRevoked
            : SessionRevocationWriteResult.NotFound;
    }

    private static string? NormalizeId(string? value)
    {
        if (!Guid.TryParseExact(
                value, "D", out var parsed) ||
            parsed == Guid.Empty)
        {
            return null;
        }

        return parsed.ToString("D");
    }
}
