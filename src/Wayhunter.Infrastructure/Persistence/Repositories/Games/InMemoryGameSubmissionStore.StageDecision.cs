using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.Games.Services;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5-MS06-T05
/// In-memory Game stage proof acceptance/rejection.
///
/// Prototype only.
/// Production authentication, SQL persistence,
/// and durable decision audits remain pending.
/// </summary>
public sealed partial class InMemoryGameSubmissionStore
    : IGameStageProofDecisionStore
{
    private readonly IGameStageProofDecisionAuthorizer?
        _stageProofDecisionAuthorizer;

    private readonly Dictionary<Guid,
        (GameStageProofDecisionCommand Command,
         string ReviewerUserId,
         GameStageProofDecisionResultDto Result)>
        _stageProofDecisionsById = new();

    private readonly HashSet<Guid>
        _decidedStageEvidenceIds = new();

    // Preserve the existing constructors.
    // Trusted reviewer authorization is required
    // for this new decision workflow.
    public InMemoryGameSubmissionStore(
        IGameProofCaptureVerifier captureVerifier,
        IGameStageProofDecisionAuthorizer decisionAuthorizer)
        : this(captureVerifier)
    {
        _stageProofDecisionAuthorizer =
            decisionAuthorizer ??
            throw new ArgumentNullException(
                nameof(decisionAuthorizer));
    }

    public async Task<GameStageProofDecisionResultDto>
        ApplyDecisionAsync(
            GameStageProofDecisionCommand command,
            string authenticatedReviewerUserId,
            DateTimeOffset reviewedAtUtc,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.DecisionId == Guid.Empty ||
            command.GameId == Guid.Empty ||
            command.AttemptId == Guid.Empty ||
            command.EvidenceId == Guid.Empty ||
            command.StepNumber <= 0)
        {
            throw new ArgumentException(
                "Invalid Game proof decision identifiers.",
                nameof(command));
        }

        if (string.IsNullOrWhiteSpace(
            authenticatedReviewerUserId))
        {
            throw new ArgumentException(
                "Authenticated reviewer is required.",
                nameof(authenticatedReviewerUserId));
        }

        if (reviewedAtUtc == default ||
            reviewedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid server UTC time is required.",
                nameof(reviewedAtUtc));
        }

        string reviewerId =
            authenticatedReviewerUserId.Trim();

        string? reason = command.Reason?.Trim();

        if (string.IsNullOrEmpty(reason))
        {
            reason = null;
        }

        if (!command.Approved && reason is null)
        {
            throw new ArgumentException(
                "Rejection reason is required.",
                nameof(command));
        }

        if (reason is { Length: > 1000 })
        {
            throw new ArgumentException(
                "Review reason exceeds 1000 characters.",
                nameof(command));
        }

        var normalized = command with
        {
            Reason = reason
        };

        // Fail closed if no trusted authorizer exists.
        // A client-provided approval flag is not enough.
        if (_stageProofDecisionAuthorizer is null)
        {
            throw new UnauthorizedAccessException(
                "Trusted reviewer authorization is not configured.");
        }

        bool authorized =
            await _stageProofDecisionAuthorizer
                .IsAuthorizedAsync(
                    reviewerId,
                    normalized.GameId,
                    cancellationToken);

        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "Reviewer is not authorized for this Game.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Shared lock protects Game state, progress,
        // proof records, and decision records.
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Identical retry returns the same result.
            if (_stageProofDecisionsById.TryGetValue(
                normalized.DecisionId,
                out var previous))
            {
                if (previous.Command == normalized &&
                    string.Equals(
                        previous.ReviewerUserId,
                        reviewerId,
                        StringComparison.Ordinal))
                {
                    return previous.Result;
                }

                throw new InvalidOperationException(
                    "Decision ID already belongs to another review.");
            }

            // One evidence item can receive only
            // one final approval/rejection decision.
            if (_decidedStageEvidenceIds.Contains(
                normalized.EvidenceId))
            {
                throw new InvalidOperationException(
                    "Game proof has already been decided.");
            }

            if (!_games.TryGetValue(
                normalized.GameId,
                out var game))
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            if (!game.ApprovedAtUtc.HasValue ||
                reviewedAtUtc < game.ApprovedAtUtc.Value)
            {
                throw new InvalidOperationException(
                    "Game approval is not effective.");
            }

            // The proof must be a real proof
            // previously stored by the backend.
            if (!_submittedGameProofs.TryGetValue(
                normalized.EvidenceId,
                out var evidence))
            {
                throw new KeyNotFoundException(
                    "Submitted Game proof not found.");
            }

            if (evidence.AttemptId !=
                    normalized.AttemptId ||
                evidence.StepNumber !=
                    normalized.StepNumber)
            {
                throw new InvalidOperationException(
                    "Proof does not match attempt or stage.");
            }

            // Resolve the authoritative player attempt.
            var matches = _gameAttempts
                .Where(pair =>
                    pair.Key.GameId == normalized.GameId &&
                    pair.Value.Attempt.Id ==
                        normalized.AttemptId)
                .Select(pair => pair.Value)
                .ToArray();

            if (matches.Length != 1)
            {
                throw new KeyNotFoundException(
                    "Game attempt not found.");
            }

            var stored = matches[0];
            var attempt = stored.Attempt;
            var progress = stored.Progress;

            // Enforces published/playable Game,
            // creator exclusion, expiry,
            // winner and moderation restrictions.
            GamePlayRules.EnsureCanStart(
                game,
                attempt.PlayerUserId,
                reviewedAtUtc);

            // The proof itself must have been
            // submitted inside the attempt window.
            if (evidence.SubmittedAtUtc <
                    attempt.StartedAtUtc ||
                evidence.SubmittedAtUtc >=
                    stored.DeadlineUtc ||
                evidence.SubmittedAtUtc >
                    reviewedAtUtc)
            {
                throw new InvalidOperationException(
                    "Proof was not submitted during the active attempt.");
            }

            // Only the exact pending stage can advance.
            if (progress.AttemptId != attempt.Id ||
                progress.Status !=
                    GameProgressStatus.WaitingForVerification ||
                progress.CurrentStepNumber !=
                    normalized.StepNumber)
            {
                throw new InvalidOperationException(
                    "Game stage is not awaiting verification.");
            }

            if (!game.DraftSteps.Any(step =>
                step.GameId == game.Id &&
                step.StepNumber == normalized.StepNumber))
            {
                throw new InvalidOperationException(
                    "Requested Game stage does not exist.");
            }

            if (normalized.Approved)
            {
                GameCompletionResult? pendingCompletion = null;

                // Prevalidate the final completion before mutating
                // Game winner or attempt progress.
                if (progress.CurrentStepNumber ==
                    progress.TotalSteps)
                {
                    pendingCompletion = PrepareCompletionResult(
                        game,
                        attempt,
                        progress,
                        evidence,
                        stored.DeadlineUtc,
                        reviewedAtUtc);

                    // Preserve existing single-winner protection.
                    game.ApproveVerifiedWinner(
                        attempt.PlayerUserId,
                        reviewedAtUtc);
                }

                // Advance stage or complete the attempt.
                progress.ApproveEvidence();

                // Record exactly one pending Admin completion
                // within the shared in-memory lock.
                if (pendingCompletion is not null)
                {
                    _completionResultsByAttemptId.Add(
                        attempt.Id,
                        pendingCompletion);
                }
            }
            else
            {
                // Retry for first two rejections; close on third.
                progress.RejectEvidence();
            }

            var result =
                new GameStageProofDecisionResultDto(
                    DecisionId: normalized.DecisionId,
                    GameId: normalized.GameId,
                    AttemptId: normalized.AttemptId,
                    EvidenceId: normalized.EvidenceId,
                    StepNumber: normalized.StepNumber,
                    Approved: normalized.Approved,
                    ReviewerUserId: reviewerId,
                    Reason: normalized.Reason,
                    ReviewedAtUtc: reviewedAtUtc,
                    CurrentStepNumber:
                        progress.CurrentStepNumber,
                    ProgressStatus: progress.Status,
                    RejectedEvidenceCount:
                        progress.RejectedEvidenceCount);

            _decidedStageEvidenceIds.Add(
                normalized.EvidenceId);

            _stageProofDecisionsById.Add(
                normalized.DecisionId,
                (normalized, reviewerId, result));

            return result;
        }
    }
}
