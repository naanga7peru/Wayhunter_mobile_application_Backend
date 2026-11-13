
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Domain.Modules.Games.Services;

public static class GamePlayRules
{
    public static void EnsureCanStart(
        Game game,
        string playerUserId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (string.IsNullOrWhiteSpace(playerUserId))
            throw new ArgumentException(
                "Player ID is required.");

        if (string.Equals(
            game.CreatorUserId.Trim(),
            playerUserId.Trim(),
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Creator cannot play their own game.");
        }

        if (!game.CanPlay(nowUtc))
            throw new InvalidOperationException(
                "Game is not available for playing.");
    }

    public static void EnsureCanSubmitEvidence(
        Game game,
        GameAttempt attempt,
        GameProgress progress,
        GameEvidence evidence,
        string playerUserId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(evidence);

        EnsureCanStart(game, playerUserId, nowUtc);

        if (attempt.GameId != game.Id)
            throw new InvalidOperationException(
                "Attempt belongs to another game.");

        if (attempt.Id != progress.AttemptId ||
            attempt.Id != evidence.AttemptId)
        {
            throw new InvalidOperationException(
                "Evidence or progress belongs to another attempt.");
        }

        if (!string.Equals(
            attempt.PlayerUserId,
            playerUserId.Trim(),
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Player does not own this attempt.");
        }

        if (progress.Status != GameProgressStatus.InProgress)
            throw new InvalidOperationException(
                "Attempt is not ready for evidence.");

        if (evidence.StepNumber != progress.CurrentStepNumber)
            throw new InvalidOperationException(
                "Evidence step does not match current step.");

        if (evidence.SubmittedAtUtc < attempt.StartedAtUtc ||
            evidence.SubmittedAtUtc > nowUtc)
        {
            throw new InvalidOperationException(
                "Invalid evidence submission time.");
        }
    }
}
