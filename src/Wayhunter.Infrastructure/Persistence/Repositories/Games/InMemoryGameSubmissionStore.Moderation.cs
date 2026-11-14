
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed partial class InMemoryGameSubmissionStore
    : IGameModerationStore
{
    // =====================================
    // M5-MS03-T05
    // IN-MEMORY MODERATION AUDIT
    // =====================================

    private readonly Dictionary<Guid,
        (GameModerationCommand Command,
         GameModerationAuditRecord Audit)>
        _moderationDecisions = new();

    // =====================================
    // APPLY REJECT / FLAG / DISABLE
    // =====================================

    public Task<GameModerationAuditRecord> ApplyAsync(
        GameModerationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Validate even if called without service.
            if (command.DecisionId == Guid.Empty ||
                command.GameId == Guid.Empty ||
                string.IsNullOrWhiteSpace(
                    command.AdminUserId) ||
                string.IsNullOrWhiteSpace(command.Reason) ||
                !Enum.IsDefined(command.Action) ||
                command.OccurredAtUtc == default ||
                command.OccurredAtUtc.Offset != TimeSpan.Zero ||
                command.CorrelationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Invalid Game moderation command.");
            }

            string cleanReason = command.Reason.Trim();

            if (cleanReason.Length > 1000)
            {
                throw new ArgumentException(
                    "Moderation reason exceeds 1000 characters.");
            }

            // Normalize for consistent retry comparison.
            var normalized = command with
            {
                AdminUserId =
                    command.AdminUserId.Trim(),

                Reason = cleanReason
            };

            // =================================
            // IDEMPOTENT DECISION RETRY
            // =================================

            if (_moderationDecisions.TryGetValue(
                normalized.DecisionId,
                out var previous))
            {
                if (previous.Command == normalized)
                {
                    return Task.FromResult(
                        previous.Audit);
                }

                throw new InvalidOperationException(
                    "Decision ID already belongs to " +
                    "another moderation request.");
            }

            // =================================
            // LOCATE GAME
            // =================================

            if (!_games.TryGetValue(
                normalized.GameId,
                out var game))
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            // =================================
            // APPLY MODERATION ACTION
            // =================================

            switch (normalized.Action)
            {
                case GameModerationAction.Reject:

                    // Rejection requires a real
                    // pending Admin queue entry.
                    if (!_pendingQueue.ContainsKey(
                        game.Id))
                    {
                        throw new InvalidOperationException(
                            "Game is not pending Admin review.");
                    }

                    // A previously approved Game
                    // cannot be rejected retroactively.
                    if (_approvals.ContainsKey(game.Id))
                    {
                        throw new InvalidOperationException(
                            "Approved Game cannot be rejected.");
                    }

                    game.RejectCreation(
                        normalized.Reason,
                        normalized.OccurredAtUtc);

                    // No creator reward is generated.
                    // Remove rejected Game from queue.
                    _pendingQueue.Remove(game.Id);

                    break;

                case GameModerationAction.Flag:

                    // Domain rules prevent:
                    // - duplicate flags
                    // - invalid Game statuses
                    // - missing reason / invalid time
                    game.FlagForReview(
                        normalized.Reason,
                        normalized.OccurredAtUtc);

                    // Pending games remain in
                    // the queue for further review.
                    // Flagged published games cannot play.
                    break;

                case GameModerationAction.Disable:

                    // Domain only allows
                    // disabling published Games.
                    game.DisableGame(
                        normalized.Reason,
                        normalized.OccurredAtUtc);

                    // Existing approved reward events
                    // are not removed automatically.
                    break;

                default:

                    throw new InvalidOperationException(
                        "Unsupported moderation action.");
            }

            // =================================
            // CREATE AUDIT RECORD
            // =================================

            var audit = new GameModerationAuditRecord(
                DecisionId: normalized.DecisionId,
                GameId: game.Id,
                AdminUserId: normalized.AdminUserId,
                Action: normalized.Action,
                Reason: normalized.Reason,
                OccurredAtUtc: normalized.OccurredAtUtc,
                ResultingStatus: game.Status,
                CorrelationId: normalized.CorrelationId
            );

            // Protected by the same process lock.
            // SQL implementation requires a durable
            // transaction for Game + audit changes.
            _moderationDecisions.Add(
                normalized.DecisionId,
                (normalized, audit));

            return Task.FromResult(audit);
        }
    }

    // =====================================
    // READ MODERATION AUDIT HISTORY
    // =====================================

    public IReadOnlyList<GameModerationAuditRecord>
        GetModerationAuditRecords()
    {
        lock (_gate)
        {
            return _moderationDecisions.Values
                .Select(item => item.Audit)
                .OrderBy(item => item.OccurredAtUtc)
                .ThenBy(item => item.DecisionId)
                .ToArray();
        }
    }

    // =====================================
    // READ ONE GAME'S AUDIT HISTORY
    // =====================================

    public IReadOnlyList<GameModerationAuditRecord>
        GetModerationAuditRecords(Guid gameId)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        lock (_gate)
        {
            return _moderationDecisions.Values
                .Select(item => item.Audit)
                .Where(item => item.GameId == gameId)
                .OrderBy(item => item.OccurredAtUtc)
                .ThenBy(item => item.DecisionId)
                .ToArray();
        }
    }
}
