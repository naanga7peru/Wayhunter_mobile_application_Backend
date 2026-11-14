
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed class InMemoryGameSubmissionStore
    : IGameSubmissionStore
{
    private readonly object _gate = new();

    private readonly Dictionary<Guid, Game> _games = new();

    private readonly Dictionary<Guid, GameAdminQueueItemDto>
        _pendingQueue = new();

    // =====================================
    // REGISTER A DRAFT FOR DEMO / TESTING
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
    // SUBMIT DRAFT AND ADD TO ADMIN QUEUE
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
                identity.ServerTimestampUtc.Offset !=
                    TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Invalid submission identity snapshot.");
            }

            // Find the original draft.
            if (!_games.TryGetValue(
                identity.ItemId,
                out var game))
            {
                throw new KeyNotFoundException(
                    "Game draft not found.");
            }

            // Reject previously submitted games.
            if (_pendingQueue.ContainsKey(game.Id))
            {
                throw new InvalidOperationException(
                    "Game already submitted.");
            }

            // The submitted identity must own the game.
            if (!string.Equals(
                game.CreatorUserId,
                identity.UserId,
                StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "Only the creator can submit this game.");
            }

            // Validate draft and change its status.
            // A failure here leaves the queue unchanged.
            game.SubmitForAdminReview(
                identity.UserId,
                identity.ServerTimestampUtc);

            // Preserve the creator's name at submission.
            var queueItem = new GameAdminQueueItemDto(
                game.Id,
                identity.UserId,
                identity.DisplayNameSnapshot,
                game.Title!,
                identity.ServerTimestampUtc);

            // Protected by the same in-memory lock.
            _pendingQueue.Add(
                game.Id,
                queueItem);

            return Task.FromResult(queueItem);
        }
    }

    // =====================================
    // READ ADMIN PENDING QUEUE
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
}
