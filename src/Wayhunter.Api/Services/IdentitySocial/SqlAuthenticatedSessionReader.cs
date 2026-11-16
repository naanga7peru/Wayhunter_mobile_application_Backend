
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class SqlAuthenticatedSessionReader
    : IAuthenticatedSessionReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlAuthenticatedSessionReader(
        WayhunterSharedDbContext db)
    {
        _db = db ??
            throw new ArgumentNullException(nameof(db));
    }

    public async Task<UserAccount?> FindAccountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryNormalizeId(userId, out var normalizedId))
        {
            return null;
        }

        return await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.UserId == normalizedId,
                cancellationToken);
    }

    public async Task<IdentitySession?> FindSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryNormalizeId(sessionId, out var normalizedId))
        {
            return null;
        }

        return await _db.IdentitySessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                session => session.SessionId == normalizedId,
                cancellationToken);
    }

    private static bool TryNormalizeId(
        string? value,
        out string normalized)
    {
        normalized = string.Empty;

        if (!Guid.TryParseExact(
                value, "D", out var parsed) ||
            parsed == Guid.Empty)
        {
            return false;
        }

        normalized = parsed.ToString("D");
        return true;
    }
}
