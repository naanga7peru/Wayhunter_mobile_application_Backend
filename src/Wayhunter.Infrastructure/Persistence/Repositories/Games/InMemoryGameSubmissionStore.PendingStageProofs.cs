
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5 Game Admin pending stage-proof review queue.
///
/// In-memory prototype only.
/// Production SQL query and Admin authorization
/// remain separate integration tasks.
/// </summary>
public sealed partial class InMemoryGameSubmissionStore
    : IGamePendingStageProofQueryStore
{
    public Task<IReadOnlyList<GamePendingStageProofDto>>
        GetPendingStageProofsAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<GamePendingStageProofDto>();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Inspect each tracked Game attempt.
            foreach (var entry in _gameAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var gameId = entry.Key.GameId;
                var stored = entry.Value;

                // Only proofs waiting for reviewer verification
                // belong in the pending review queue.
                if (stored.Progress.Status !=
                    GameProgressStatus.WaitingForVerification)
                {
                    continue;
                }

                if (!_games.TryGetValue(
                    gameId,
                    out var game))
                {
                    continue;
                }

                // Locate the evidence for the current stage.
                var pendingEvidence = _submittedGameProofs.Values
                    .Where(evidence =>
                        evidence.AttemptId == stored.Attempt.Id &&
                        evidence.StepNumber ==
                            stored.Progress.CurrentStepNumber &&
                        !_decidedStageEvidenceIds.Contains(
                            evidence.Id))
                    .OrderBy(evidence =>
                        evidence.SubmittedAtUtc)
                    .ThenBy(evidence =>
                        evidence.Id)
                    .ToArray();

                foreach (var evidence in pendingEvidence)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    results.Add(
                        new GamePendingStageProofDto(
                            GameId: game.Id,
                            GameTitle: game.Title!,
                            PlayerUserId:
                                stored.Attempt.PlayerUserId,
                            AttemptId: stored.Attempt.Id,
                            EvidenceId: evidence.Id,
                            StepNumber: evidence.StepNumber,
                            MediaId: evidence.MediaId,
                            SubmittedAtUtc:
                                evidence.SubmittedAtUtc,
                            AttemptStartedAtUtc:
                                stored.Attempt.StartedAtUtc,
                            AttemptDeadlineUtc:
                                stored.DeadlineUtc));
                }
            }

            // Stable ordering for Admin queue pagination
            // and predictable testing.
            IReadOnlyList<GamePendingStageProofDto> snapshot =
                results
                    .OrderBy(item => item.SubmittedAtUtc)
                    .ThenBy(item => item.GameId)
                    .ThenBy(item => item.EvidenceId)
                    .ToArray();

            return Task.FromResult(snapshot);
        }
    }
}
