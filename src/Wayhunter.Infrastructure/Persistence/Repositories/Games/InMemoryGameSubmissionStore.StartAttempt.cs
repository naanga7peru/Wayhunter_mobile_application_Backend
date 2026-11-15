
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
/// M5-MS04-T05 / M5-MS05-T03
/// In-memory Game Attempt start and resume.
/// Uses shared server deadline validation rules.
///
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

        // =====================================
        // VALIDATE REQUEST
        // =====================================

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

            // =====================================
            // LOAD GAME
            // =====================================

            if (!_games.TryGetValue(gameId, out var game))
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            // Reuse existing Game eligibility rules.
            // Creator, expired, disabled, flagged and
            // winner-closed Games are blocked.
            GamePlayRules.EnsureCanStart(
                game,
                cleanPlayerId,
                nowUtc);

            // Admin approval must be effective.
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

            // =====================================
            // EXISTING ATTEMPT - RESUME
            // =====================================

            if (_gameAttempts.TryGetValue(
                key,
                out var existing))
            {
                // Validate both attempt start time
                // and deadline using shared rules.
                GameAttemptDeadlineRules.EnsureActive(
                    existing.Attempt.StartedAtUtc,
                    existing.DeadlineUtc,
                    nowUtc);

                // Only active attempts can resume.
                if (existing.Progress.Status !=
                        GameProgressStatus.InProgress &&
                    existing.Progress.Status !=
                        GameProgressStatus.WaitingForVerification)
                {
                    throw new InvalidOperationException(
                        "Existing Game attempt is not active.");
                }

                cancellationToken.ThrowIfCancellationRequested();

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

            // =====================================
            // CALCULATE SERVER DEADLINE
            // =====================================

            // Deadline cannot exceed Game expiry.
            // Reuses shared deadline calculation.
            var deadlineUtc =
                GameAttemptDeadlineRules.CalculateDeadlineUtc(
                    nowUtc,
                    game.DurationMinutes.Value,
                    game.ExpiresAtUtc.Value);

            // =====================================
            // CREATE NEW ATTEMPT
            // =====================================

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

            // Store attempt, progress and deadline.
            // In-memory prototype only.
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
