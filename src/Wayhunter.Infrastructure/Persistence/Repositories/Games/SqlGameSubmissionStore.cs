using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed class SqlGameSubmissionStore : IGameSubmissionStore
{
    private readonly WayhunterSharedDbContext _db;
    private readonly SqlGameRepository _games;

    public SqlGameSubmissionStore(WayhunterSharedDbContext db, SqlGameRepository games)
    {
        _db = db;
        _games = games;
    }

    public async Task<GameAdminQueueItemDto> SubmitDraftAndEnqueueAsync(
        ApprovalIdentitySnapshotDto identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, cancellationToken);
                var row = await _db.Games.FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Games] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {identity.ItemId}")
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException("Game draft not found.");
                if (!string.Equals(row.CreatorUserId, identity.UserId, StringComparison.Ordinal))
                    throw new UnauthorizedAccessException("Only the creator can submit this draft.");
                if (row.Status != GameStatus.Draft)
                    throw new InvalidOperationException("Only drafts can be submitted for review.");
                var game = await _games.GetByIdAsync(identity.ItemId, cancellationToken)
                    ?? throw new KeyNotFoundException("Game draft not found.");
                game.SubmitForAdminReview(identity.UserId, identity.ServerTimestampUtc);
                row.Status = game.Status;
                row.SubmittedAtUtc = game.SubmittedAtUtc;
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return new GameAdminQueueItemDto(game.Id, identity.UserId,
                    identity.DisplayNameSnapshot, game.Title!, identity.ServerTimestampUtc);
            }
            catch
            {
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
