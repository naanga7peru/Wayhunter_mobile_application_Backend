using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

/// <summary>
/// M6 adapter over M1's authoritative persisted account record.
/// It never accepts a client-provided verification flag.
/// </summary>
public sealed class SqlVerifiedWelcomeAccountReader
    : IVerifiedWelcomeAccountReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlVerifiedWelcomeAccountReader(
        WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<UserAccount?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        string normalizedUserId = userId.Trim();

        return _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == normalizedUserId,
                cancellationToken);
    }
}
