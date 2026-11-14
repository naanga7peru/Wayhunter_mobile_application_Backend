
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed class InMemoryGameSubmissionStore
    : IGameSubmissionStore, IGameApprovalStore
{
    private readonly object _gate = new();

    private readonly Dictionary<Guid, Game> _games = new();

    private readonly Dictionary<Guid, GameAdminQueueItemDto>
        _pendingQueue = new();

    private readonly Dictionary<Guid,
        (GameApprovalCommand Command,
         CoinRewardGrantRequest Reward)> _approvals = new();

    // =====================================
    // REGISTER GAME DRAFT
    // =====================================

    public void AddDraft(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        lock (_gate)
        {
            if (game.Status != GameStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft games can be registered.");
            }

            _games.Add(game.Id, game);
        }
    }

    // =====================================
    // SUBMIT GAME TO ADMIN QUEUE
    // =====================================

    public Task<GameAdminQueueItemDto>
        SubmitDraftAndEnqueueAsync(
            ApprovalIdentitySnapshotDto identity,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (identity.ItemId == Guid.Empty ||
                string.IsNullOrWhiteSpace(identity.UserId) ||
                string.IsNullOrWhiteSpace(
                    identity.DisplayNameSnapshot) ||
                identity.ServerTimestampUtc == default ||
                identity.ServerTimestampUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Invalid submission identity snapshot.");
            }

            if (!_games.TryGetValue(identity.ItemId, out var game))
            {
                throw new KeyNotFoundException(
                    "Game draft not found.");
            }

            if (_pendingQueue.ContainsKey(game.Id))
            {
                throw new InvalidOperationException(
                    "Game already submitted.");
            }

            if (!string.Equals(
                game.CreatorUserId,
                identity.UserId,
                StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "Only the creator can submit this game.");
            }

            game.SubmitForAdminReview(
                identity.UserId,
                identity.ServerTimestampUtc);

            var queueItem = new GameAdminQueueItemDto(
                game.Id,
                identity.UserId,
                identity.DisplayNameSnapshot,
                game.Title!,
                identity.ServerTimestampUtc);

            _pendingQueue.Add(game.Id, queueItem);

            return Task.FromResult(queueItem);
        }
    }

    // =====================================
    // ADMIN APPROVE GAME + REWARD OUTBOX
    // M5-MS03-T04
    // =====================================

    public Task<CoinRewardGrantRequest>
        ApproveAndEnqueueCreatorRewardAsync(
            GameApprovalCommand command,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Validate the approval command even when
            // the store is called without the service.
            if (command.GameId == Guid.Empty ||
                string.IsNullOrWhiteSpace(command.AdminUserId) ||
                string.IsNullOrWhiteSpace(command.ApprovalRecordId) ||
                command.ApprovedAtUtc == default ||
                command.ApprovedAtUtc.Offset != TimeSpan.Zero ||
                command.RewardEventId == Guid.Empty ||
                command.CorrelationId == Guid.Empty ||
                string.IsNullOrWhiteSpace(command.RewardRuleVersion))
            {
                throw new ArgumentException(
                    "Invalid Game approval command.");
            }

            if (!_games.TryGetValue(command.GameId, out var game))
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            // Identical retries return the existing event.
            // Conflicting approvals are rejected.
            if (_approvals.TryGetValue(
                command.GameId, out var previous))
            {
                if (previous.Command == command)
                {
                    return Task.FromResult(previous.Reward);
                }

                throw new InvalidOperationException(
                    "Game has already been approved.");
            }

            if (!_pendingQueue.TryGetValue(
                game.Id, out var queueItem))
            {
                throw new InvalidOperationException(
                    "Game is not in the Admin pending queue.");
            }

            if (game.Status != GameStatus.PendingApproval ||
                !game.SubmittedAtUtc.HasValue)
            {
                throw new InvalidOperationException(
                    "Only submitted pending games can be approved.");
            }

            if (!string.Equals(
                queueItem.CreatorUserId,
                game.CreatorUserId,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Game creator identity mismatch.");
            }

            if (command.ApprovedAtUtc <
                game.SubmittedAtUtc.Value)
            {
                throw new InvalidOperationException(
                    "Approval cannot precede submission.");
            }

            // Validate expiry range before changing state.
            _ = command.ApprovedAtUtc.AddDays(30);

            // Validate M6 request before publishing.
            var preflightReward = new CoinRewardGrantRequest(
                EventId: command.RewardEventId,
                RewardType: CoinRewardType.GameCreatorApproved,
                SourceRecordId: game.Id.ToString("D"),
                RecipientUserId: game.CreatorUserId,
                CoinsAmount: CoinRewardRules.GetAmount(
                    CoinRewardType.GameCreatorApproved),
                EligibilityConfirmedAtUtc: command.ApprovedAtUtc,
                CorrelationId: command.CorrelationId,
                RuleVersion: command.RewardRuleVersion,
                ApprovalRecordId: command.ApprovalRecordId
            );

            CoinRewardContractRules.ValidateGrant(
                preflightReward);

            // Publish the game.
            // Game domain calculates 30-day expiry.
            game.ApproveCreation(command.ApprovedAtUtc);

            // Produce the authoritative M6 reward request.
            var reward =
                GameCreatorApprovalRewardFactory.Create(
                    game,
                    command.ApprovalRecordId,
                    command.RewardEventId,
                    command.CorrelationId,
                    command.RewardRuleVersion);

            // In-process demo storage.
            // A SQL implementation must use a transaction
            // and durable outbox with unique constraints.
            _approvals.Add(
                game.Id,
                (command, reward));

            // Remove the approved game from pending review.
            _pendingQueue.Remove(game.Id);

            return Task.FromResult(reward);
        }
    }

    // =====================================
    // READ PENDING ADMIN QUEUE
    // =====================================

    public IReadOnlyList<GameAdminQueueItemDto>
        GetPendingItems()
    {
        lock (_gate)
        {
            return _pendingQueue.Values
                .OrderBy(item => item.SubmittedAtUtc)
                .ToArray();
        }
    }

    // =====================================
    // READ DEMO REWARD OUTBOX
    // =====================================

    public IReadOnlyList<CoinRewardGrantRequest>
        GetCreatorRewardOutbox()
    {
        lock (_gate)
        {
            return _approvals.Values
                .Select(item => item.Reward)
                .ToArray();
        }
    }
}
