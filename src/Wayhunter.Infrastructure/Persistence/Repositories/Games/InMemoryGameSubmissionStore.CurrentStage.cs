
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.Games.Services;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5-MS05-T01
/// Current Game Stage query.
///
/// Returns only the current stage and its hints.
/// SQL persistence remains on migration HOLD.
/// </summary>
public sealed partial class InMemoryGameSubmissionStore
    : ICurrentGameStageQueryStore
{
    public Task<CurrentGameStageDto> GetCurrentStageAsync(
        Guid gameId,
        Guid attemptId,
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

        if (attemptId == Guid.Empty)
        {
            throw new ArgumentException(
                "Attempt ID is required.",
                nameof(attemptId));
        }

        if (string.IsNullOrWhiteSpace(playerUserId))
        {
            throw new ArgumentException(
                "Player User ID is required.",
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

            // Reuse existing eligibility rules.
            // This also blocks the Game creator.
            GamePlayRules.EnsureCanStart(
                game,
                cleanPlayerId,
                nowUtc);

            // No access before Admin approval.
            if (!game.ApprovedAtUtc.HasValue ||
                game.ApprovedAtUtc.Value > nowUtc)
            {
                throw new InvalidOperationException(
                    "Game approval is not yet effective.");
            }

            // =====================================
            // LOAD AUTHORIZED ATTEMPT
            // =====================================

            var key = (gameId, cleanPlayerId);

            if (!_gameAttempts.TryGetValue(
                key,
                out var stored))
            {
                throw new KeyNotFoundException(
                    "Game attempt not found.");
            }

            var attempt = stored.Attempt;
            var progress = stored.Progress;

            // Prevent access to another attempt.
            if (attempt.Id != attemptId ||
                attempt.GameId != gameId ||
                !string.Equals(
                    attempt.PlayerUserId,
                    cleanPlayerId,
                    StringComparison.Ordinal))
            {
                throw new KeyNotFoundException(
                    "Game attempt not found.");
            }

            // =====================================
            // VALIDATE ATTEMPT DEADLINE
            // =====================================

            if (nowUtc < attempt.StartedAtUtc ||
                nowUtc >= stored.DeadlineUtc)
            {
                throw new InvalidOperationException(
                    "Game attempt is outside its active time.");
            }

            // Only active progress can view clues.
            if (progress.Status !=
                    GameProgressStatus.InProgress &&
                progress.Status !=
                    GameProgressStatus.WaitingForVerification)
            {
                throw new InvalidOperationException(
                    "Game attempt is not active.");
            }

            if (progress.AttemptId != attempt.Id)
            {
                throw new InvalidOperationException(
                    "Game progress does not match attempt.");
            }

            // =====================================
            // CURRENT STAGE ONLY
            // =====================================

            int currentStepNumber =
                progress.CurrentStepNumber;

            var currentStep = game.DraftSteps
                .SingleOrDefault(step =>
                    step.GameId == game.Id &&
                    step.StepNumber == currentStepNumber);

            if (currentStep is null)
            {
                throw new InvalidOperationException(
                    "Current Game stage not found.");
            }

            // Security:
            // Never return future stage hints.
            var currentHints = game.DraftHints
                .Where(hint =>
                    hint.GameStepId == currentStep.Id)
                .Select(hint =>
                    new CurrentStageHintDto(
                        TextHint: hint.TextHint,
                        PhotoMediaId: hint.PhotoMediaId))
                .ToArray();

            if (currentHints.Length == 0)
            {
                throw new InvalidOperationException(
                    "Current stage has no hints.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // =====================================
            // RETURN SAFE CURRENT STAGE
            // =====================================

            CurrentGameStageDto result = new(
                AttemptId: attempt.Id,
                GameId: game.Id,
                GameStepId: currentStep.Id,
                CurrentStepNumber: currentStepNumber,
                TotalSteps: progress.TotalSteps,
                ProgressStatus: progress.Status,
                DeadlineUtc: stored.DeadlineUtc,
                Hints: currentHints
            );

            return Task.FromResult(result);
        }
    }
}
