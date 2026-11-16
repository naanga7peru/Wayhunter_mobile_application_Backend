
using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.Games.Services;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5-MS07-T01 — In-memory Game completion results.
/// One PendingAdmin result per completed Attempt.
/// No Coins grants or SQL persistence.
/// </summary>
public sealed partial class InMemoryGameSubmissionStore
{
    private readonly Dictionary<Guid, GameCompletionResult>
        _completionResultsByAttemptId = new();

    public GameCompletionResult? GetCompletionResult(
        Guid attemptId)
    {
        if (attemptId == Guid.Empty)
        {
            throw new ArgumentException(
                "Attempt ID is required.",
                nameof(attemptId));
        }

        lock (_gate)
        {
            return _completionResultsByAttemptId.TryGetValue(
                attemptId,
                out var result)
                ? result
                : null;
        }
    }

    public IReadOnlyList<GameCompletionResult>
        GetCompletionResults()
    {
        lock (_gate)
        {
            return _completionResultsByAttemptId.Values
                .OrderBy(result => result.CompletedAtUtc)
                .ThenBy(result => result.AttemptId)
                .ToArray();
        }
    }

    /// <summary>
    /// Prepares a completion result before final-stage
    /// state mutation. Caller must hold _gate.
    ///
    /// The current final-stage decision has not yet
    /// been stored, so it is validated separately.
    /// </summary>
    private GameCompletionResult PrepareCompletionResult(
        Game game,
        GameAttempt attempt,
        GameProgress progress,
        GameEvidence finalEvidence,
        DateTimeOffset deadlineUtc,
        DateTimeOffset reviewedAtUtc)
    {
        if (game.Id != attempt.GameId ||
            progress.AttemptId != attempt.Id ||
            finalEvidence.AttemptId != attempt.Id)
        {
            throw new InvalidOperationException(
                "Game completion identity mismatch.");
        }

        if (string.Equals(
            game.CreatorUserId,
            attempt.PlayerUserId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Game creator cannot complete their own game.");
        }

        if (_completionResultsByAttemptId.ContainsKey(
            attempt.Id))
        {
            throw new InvalidOperationException(
                "Completion result already exists for this attempt.");
        }

        GameAttemptDeadlineRules.EnsureActive(
            attempt.StartedAtUtc,
            deadlineUtc,
            reviewedAtUtc);

        if (progress.Status !=
                GameProgressStatus.WaitingForVerification ||
            progress.CurrentStepNumber != progress.TotalSteps ||
            finalEvidence.StepNumber != progress.TotalSteps ||
            game.DraftSteps.Count != progress.TotalSteps)
        {
            throw new InvalidOperationException(
                "Game final stage is not ready for completion.");
        }

        var approvedEvidenceIds = new List<Guid>();

        for (int stageNumber = 1;
             stageNumber <= progress.TotalSteps;
             stageNumber++)
        {
            GameEvidence approvedEvidence;

            if (stageNumber == progress.TotalSteps)
            {
                approvedEvidence = finalEvidence;
            }
            else
            {
                var approvedDecisions =
                    _stageProofDecisionsById.Values
                        .Where(entry =>
                            entry.Command.GameId == game.Id &&
                            entry.Command.AttemptId == attempt.Id &&
                            entry.Command.StepNumber == stageNumber &&
                            entry.Command.Approved)
                        .ToArray();

                if (approvedDecisions.Length != 1)
                {
                    throw new InvalidOperationException(
                        "Every required Game stage must have exactly one approved proof.");
                }

                var approvedEvidenceId =
                    approvedDecisions[0].Command.EvidenceId;

                if (!_submittedGameProofs.TryGetValue(
                    approvedEvidenceId,
                    out var storedEvidence))
                {
                    throw new InvalidOperationException(
                        "Approved stage evidence is missing.");
                }

                approvedEvidence = storedEvidence;
            }

            if (approvedEvidence.AttemptId != attempt.Id ||
                approvedEvidence.StepNumber != stageNumber ||
                approvedEvidence.SubmittedAtUtc <
                    attempt.StartedAtUtc ||
                approvedEvidence.SubmittedAtUtc >= deadlineUtc ||
                approvedEvidence.SubmittedAtUtc > reviewedAtUtc)
            {
                throw new InvalidOperationException(
                    "Approved stage proof is outside the valid attempt window.");
            }

            approvedEvidenceIds.Add(approvedEvidence.Id);
        }

        return new GameCompletionResult(
            Guid.NewGuid(),
            game.Id,
            attempt.Id,
            attempt.PlayerUserId,
            approvedEvidenceIds,
            attempt.StartedAtUtc,
            deadlineUtc,
            reviewedAtUtc);
    }
}
