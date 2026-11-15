
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.Games.Services;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5-MS04-T05
/// In-memory Game Attempt start and resume.
/// SQL persistence remains on migration HOLD.
/// </summary>
public sealed partial class InMemoryGameSubmissionStore
    : IStartGameAttemptStore
{
    // One tracked attempt per Game and player.
    // Access is protected by the existing _gate lock.
    private readonly Dictionary<
        (Guid GameId, string PlayerUserId),
        (
            GameAttempt Attempt,
            GameProgress Progress,
            DateTimeOffset DeadlineUtc
        )> _gameAttempts = new();

    public Task<StartGameAttemptResultDto> StartOrResumeAsync(
        Guid gameId,
        string playerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Validate request.
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        if (string.IsNullOrWhiteSpace(playerUserId))
        {
            throw new ArgumentException(
                "Player ID is required.",
                nameof(playerUserId));
        }

        if (nowUtc == default ||
            nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid server UTC time is required.",
                nameof(nowUtc));
        }

        string cleanPlayerId = playerUserId.Trim();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find requested game.
            if (!_games.TryGetValue(gameId, out var game))
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            // Existing domain rules:
            // - Creator cannot play own game.
            // - Published Game only.
            // - No expired or disabled Game.
            // - No flagged or winner-closed Game.
            GamePlayRules.EnsureCanStart(
                game,
                cleanPlayerId,
                nowUtc);

            // Do not allow play before approval time.
            if (!game.ApprovedAtUtc.HasValue ||
                game.ApprovedAtUtc.Value > nowUtc)
            {
                throw new InvalidOperationException(
                    "Game approval is not yet effective.");
            }

            if (game.DurationMinutes is null or <= 0 ||
                !game.ExpiresAtUtc.HasValue ||
                game.DraftSteps.Count == 0)
            {
                throw new InvalidOperationException(
                    "Game play configuration is incomplete.");
            }

            var key = (gameId, cleanPlayerId);

            // =================================
            // EXISTING ATTEMPT
            // =================================

            if (_gameAttempts.TryGetValue(
                key,
                out var existing))
            {
                // An expired attempt must not resume.
                if (nowUtc >= existing.DeadlineUtc)
                {
                    throw new InvalidOperationException(
                        "Existing Game attempt deadline expired.");
                }

                // Only an active attempt can resume.
                if (existing.Progress.Status !=
                        GameProgressStatus.InProgress &&
                    existing.Progress.Status !=
                        GameProgressStatus.WaitingForVerification)
                {
                    throw new InvalidOperationException(
                        "Existing Game attempt is not active.");
                }

                return Task.FromResult(
                    new StartGameAttemptResultDto(
                        AttemptId: existing.Attempt.Id,
                        GameId: existing.Attempt.GameId,
                        PlayerUserId:
                            existing.Attempt.PlayerUserId,
                        StartedAtUtc:
                            existing.Attempt.StartedAtUtc,
                        DeadlineUtc: existing.DeadlineUtc,
                        CurrentStepNumber:
                            existing.Progress.CurrentStepNumber,
                        ResumedExistingAttempt: true));
            }

            // =================================
            // CALCULATE SERVER DEADLINE
            // =================================

            var remainingTime =
                game.ExpiresAtUtc.Value - nowUtc;

            if (remainingTime <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "Game has already expired.");
            }

            var requestedDuration = TimeSpan.FromMinutes(
                game.DurationMinutes.Value);

            // Deadline must not exceed Game expiry.
            var allowedDuration =
                requestedDuration <= remainingTime
                    ? requestedDuration
                    : remainingTime;

            var deadlineUtc =
                nowUtc.Add(allowedDuration);

            // =================================
            // CREATE NEW ATTEMPT
            // =================================

            var attempt = new GameAttempt(
                Guid.NewGuid(),
                game.Id,
                cleanPlayerId,
                nowUtc);

            var progress = new GameProgress(
                attempt.Id,
                game.DraftSteps.Count);

            progress.Start();

            cancellationToken.ThrowIfCancellationRequested();

            // Attempt and progress are inserted
            // into the same in-memory record.
            _gameAttempts.Add(
                key,
                (attempt, progress, deadlineUtc));

            return Task.FromResult(
                new StartGameAttemptResultDto(
                    AttemptId: attempt.Id,
                    GameId: attempt.GameId,
                    PlayerUserId: attempt.PlayerUserId,
                    StartedAtUtc: attempt.StartedAtUtc,
                    DeadlineUtc: deadlineUtc,
                    CurrentStepNumber:
                        progress.CurrentStepNumber,
                    ResumedExistingAttempt: false));
        }
    }
}
