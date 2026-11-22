using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.IdentitySocial;

/// <summary>
/// Read-only lookup of an authoritative M1 account by immutable UserId.
/// Deliberately not registered in production DI until the M1 owner approves
/// this persistence source and identity mapping.
/// </summary>
public sealed class SqlTrustedIdentityAccountReader
    : ITrustedIdentityAccountReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlTrustedIdentityAccountReader(
        WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<UserAccount?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Guid.TryParseExact(userId?.Trim(), "D", out var parsedId) ||
            parsedId == Guid.Empty)
        {
            return null;
        }

        var canonicalUserId = parsedId.ToString("D");

        try
        {
            var account = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    user => user.UserId == canonicalUserId,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return account;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
            when (ex is not OutOfMemoryException)
        {
            // Missing table, unavailable database, or ambiguous/query failure
            // must never produce trusted identity eligibility.
            return null;
        }
    }
}
