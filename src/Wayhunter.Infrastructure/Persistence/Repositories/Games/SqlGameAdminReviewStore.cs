using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed class SqlGameAdminReviewStore : IGameApprovalStore, IGameModerationStore
{
    private readonly WayhunterSharedDbContext _db;
    private readonly SqlGameRepository _games;

    public SqlGameAdminReviewStore(WayhunterSharedDbContext db, SqlGameRepository games)
    {
        _db = db;
        _games = games;
    }

    public async Task<IReadOnlyList<GameAdminQueueItemDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var rows = await (from game in _db.Games.AsNoTracking()
                          join user in _db.Users.AsNoTracking() on game.CreatorUserId equals user.UserId
                          where game.Status == GameStatus.PendingApproval && game.SubmittedAtUtc != null
                          orderby game.SubmittedAtUtc, game.Id
                          select new { game.Id, game.CreatorUserId, user.DisplayName, game.Title, SubmittedAtUtc = game.SubmittedAtUtc })
            .ToListAsync(ct);
        return rows.Select(x => new GameAdminQueueItemDto(
            x.Id, x.CreatorUserId, x.DisplayName, x.Title ?? string.Empty, x.SubmittedAtUtc!.Value)).ToArray();
    }

    public async Task<CoinRewardGrantRequest> ApproveAndEnqueueCreatorRewardAsync(
        GameApprovalCommand command, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, ct);
                var existing = await _db.GameCreationApprovals.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.GameId == command.GameId, ct);
                if (existing is not null)
                {
                    if (existing.AdminUserId != command.AdminUserId || existing.ApprovalRecordId != command.ApprovalRecordId ||
                        existing.RewardEventId != command.RewardEventId || existing.CorrelationId != command.CorrelationId)
                        throw new InvalidOperationException("Game already has a different approval decision.");
                    var approved = await _games.GetByIdAsync(command.GameId, ct)
                        ?? throw new KeyNotFoundException("Game not found.");
                    await tx.CommitAsync(ct);
                    return GameCreatorApprovalRewardFactory.Create(approved, existing.ApprovalRecordId,
                        existing.RewardEventId, existing.CorrelationId, existing.RewardRuleVersion);
                }

                var game = await _games.GetByIdAsync(command.GameId, ct)
                    ?? throw new KeyNotFoundException("Game not found.");
                if (game.SubmittedAtUtc is null)
                    throw new InvalidOperationException("Only submitted games can be approved.");
                game.ApproveCreation(command.ApprovedAtUtc);
                var row = await _db.Games.FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Games] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {command.GameId}").SingleAsync(ct);
                if (row.Status != GameStatus.PendingApproval)
                    throw new InvalidOperationException("Game is not pending Admin review.");
                row.Status = game.Status;
                row.ApprovedAtUtc = game.ApprovedAtUtc;
                row.ExpiresAtUtc = game.ExpiresAtUtc;
                row.LastModeratedAtUtc = command.ApprovedAtUtc;
                _db.GameCreationApprovals.Add(new GameCreationApprovalRow
                {
                    ApprovalRecordId = command.ApprovalRecordId, GameId = command.GameId,
                    AdminUserId = command.AdminUserId, ApprovedAtUtc = command.ApprovedAtUtc,
                    RewardEventId = command.RewardEventId, CorrelationId = command.CorrelationId,
                    RewardRuleVersion = command.RewardRuleVersion
                });
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return GameCreatorApprovalRewardFactory.Create(game, command.ApprovalRecordId,
                    command.RewardEventId, command.CorrelationId, command.RewardRuleVersion);
            }
            catch
            {
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task<GameModerationAuditRecord> ApplyAsync(GameModerationCommand command, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            try
            {
                await using var tx = await _db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable, ct);
                var prior = await _db.GameModerationDecisions.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.DecisionId == command.DecisionId, ct);
                if (prior is not null)
                {
                    if (prior.GameId != command.GameId || prior.AdminUserId != command.AdminUserId || prior.Action != command.Action ||
                        prior.Reason != command.Reason || prior.CorrelationId != command.CorrelationId)
                        throw new InvalidOperationException("Decision ID was used for a different moderation request.");
                    await tx.CommitAsync(ct);
                    return ToAudit(prior);
                }
                var game = await _games.GetByIdAsync(command.GameId, ct)
                    ?? throw new KeyNotFoundException("Game not found.");
                switch (command.Action)
                {
                    case GameModerationAction.Reject: game.RejectCreation(command.Reason, command.OccurredAtUtc); break;
                    case GameModerationAction.Flag: game.FlagForReview(command.Reason, command.OccurredAtUtc); break;
                    case GameModerationAction.Disable: game.DisableGame(command.Reason, command.OccurredAtUtc); break;
                    default: throw new ArgumentException("Unsupported moderation action.");
                }
                var row = await _db.Games.FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Games] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {command.GameId}").SingleAsync(ct);
                if (command.Action == GameModerationAction.Reject && row.Status != GameStatus.PendingApproval)
                    throw new InvalidOperationException("Game is not pending Admin review.");
                row.Status = game.Status;
                row.IsFlaggedForReview = game.IsFlaggedForReview;
                row.FlagReason = game.FlagReason;
                row.RejectionReason = game.RejectionReason;
                row.DisabledReason = game.DisabledReason;
                row.LastModeratedAtUtc = game.LastModeratedAtUtc;
                var audit = new GameModerationDecisionRow
                {
                    DecisionId = command.DecisionId, GameId = game.Id, AdminUserId = command.AdminUserId,
                    Action = command.Action, Reason = command.Reason.Trim(), OccurredAtUtc = command.OccurredAtUtc,
                    ResultingStatus = game.Status, CorrelationId = command.CorrelationId
                };
                _db.GameModerationDecisions.Add(audit);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return ToAudit(audit);
            }
            catch
            {
                _db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private static GameModerationAuditRecord ToAudit(GameModerationDecisionRow row) => new(
        row.DecisionId, row.GameId, row.AdminUserId, row.Action, row.Reason,
        row.OccurredAtUtc, row.ResultingStatus, row.CorrelationId);
}
