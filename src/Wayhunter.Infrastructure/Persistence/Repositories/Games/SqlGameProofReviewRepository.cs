
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.Games.Services;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5 SQL proof review and progress persistence.
///
/// Supports non-final stage approval and rejection.
/// Final-stage completion is blocked until its dedicated
/// SQL completion and winner policy is integrated.
///
/// Uses existing schema. No migrations or production DI.
/// </summary>
public sealed class SqlGameProofReviewRepository
    : IGameStageProofDecisionStore
{
    private readonly WayhunterSharedDbContext _db;
    private readonly IGameStageProofDecisionAuthorizer _authorizer;

    public SqlGameProofReviewRepository(
        WayhunterSharedDbContext db,
        IGameStageProofDecisionAuthorizer authorizer)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _authorizer = authorizer ??
            throw new ArgumentNullException(nameof(authorizer));
    }

    public async Task<GameStageProofDecisionResultDto> ApplyDecisionAsync(
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

        if (string.IsNullOrWhiteSpace(authenticatedReviewerUserId))
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

        string reviewerId = authenticatedReviewerUserId.Trim();

        string? reason = string.IsNullOrWhiteSpace(command.Reason)
            ? null
            : command.Reason.Trim();

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

        if (!_db.Database.IsSqlServer())
        {
            throw new InvalidOperationException(
                "Game proof review persistence requires SQL Server.");
        }

        // Authorization must never depend on a client-side flag.
        bool authorized = await _authorizer.IsAuthorizedAsync(
            reviewerId,
            command.GameId,
            cancellationToken);

        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "Reviewer is not authorized for this Game.");
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            // Serialize review decisions for this Game.
            var lockedGame = await _db.Games
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Games] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {command.GameId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (lockedGame is null)
            {
                throw new KeyNotFoundException("Game not found.");
            }

            var evidence = await _db.GameProofEvidence
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == command.EvidenceId,
                    cancellationToken);

            if (evidence is null)
            {
                throw new KeyNotFoundException(
                    "Submitted Game proof not found.");
            }

            if (evidence.GameId != command.GameId ||
                evidence.AttemptId != command.AttemptId ||
                evidence.StepNumber != command.StepNumber)
            {
                throw new InvalidOperationException(
                    "Proof does not match Game, attempt or stage.");
            }

            var attempt = await _db.GameAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == command.AttemptId &&
                         x.GameId == command.GameId,
                    cancellationToken);

            if (attempt is null)
            {
                throw new KeyNotFoundException(
                    "Game attempt not found.");
            }

            var progress = await _db.GameAttemptProgress
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.AttemptId == command.AttemptId,
                    cancellationToken);

            if (progress is null ||
                progress.GameId != command.GameId ||
                progress.PlayerUserId != attempt.PlayerUserId)
            {
                throw new InvalidOperationException(
                    "Game attempt progress is invalid.");
            }

            // A retry must not execute the transition twice.
            var existing = await _db.GameProofReviewDecisions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.DecisionId == command.DecisionId,
                    cancellationToken);

            if (existing is not null)
            {
                if (existing.EvidenceId != command.EvidenceId ||
                    existing.IsApproved != command.Approved ||
                    existing.ReviewedAtUtc != reviewedAtUtc ||
                    !string.Equals(
                        existing.ReviewerUserId,
                        reviewerId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        existing.Reason,
                        reason,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Decision ID already belongs to another review.");
                }

                var replayResult = await RestoreDecisionResultAsync(
                    existing,
                    evidence,
                    progress.TotalSteps,
                    command.GameId,
                    command.AttemptId,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return replayResult;
            }

            var gameRepository = new SqlGameRepository(_db);
            var game = await gameRepository.GetByIdAsync(
                command.GameId,
                cancellationToken);

            if (game is null)
            {
                throw new KeyNotFoundException("Game not found.");
            }

            GamePlayRules.EnsureCanStart(
                game,
                attempt.PlayerUserId,
                reviewedAtUtc);

            if (!game.ApprovedAtUtc.HasValue ||
                reviewedAtUtc < game.ApprovedAtUtc.Value)
            {
                throw new InvalidOperationException(
                    "Game approval is not effective.");
            }

            if (!attempt.DeadlineUtc.HasValue ||
                evidence.SubmittedAtUtc < attempt.StartedAtUtc ||
                evidence.SubmittedAtUtc >= attempt.DeadlineUtc.Value ||
                evidence.SubmittedAtUtc > reviewedAtUtc)
            {
                throw new InvalidOperationException(
                    "Proof was not submitted during the active attempt.");
            }

            if (progress.Status !=
                    GameProgressStatus.WaitingForVerification ||
                progress.CurrentStepNumber != command.StepNumber ||
                progress.TotalSteps != game.DraftSteps.Count ||
                !game.DraftSteps.Any(x =>
                    x.GameId == command.GameId &&
                    x.StepNumber == command.StepNumber))
            {
                throw new InvalidOperationException(
                    "Game stage is not awaiting verification.");
            }

            // Final-stage approval must also create the
            // PendingAdmin completion and evidence links.
            // Do not silently mark it completed here.
            if (command.Approved &&
                progress.CurrentStepNumber == progress.TotalSteps)
            {
                throw new InvalidOperationException(
                    "Final-stage approval requires the dedicated " +
                    "SQL completion workflow.");
            }

            int nextRejectionCount =
                progress.RejectedEvidenceCount +
                (command.Approved ? 0 : 1);

            int nextStep = command.Approved
                ? progress.CurrentStepNumber + 1
                : progress.CurrentStepNumber;

            var nextStatus = command.Approved
                ? GameProgressStatus.InProgress
                : nextRejectionCount >= 3
                    ? GameProgressStatus.Rejected
                    : GameProgressStatus.InProgress;

            // Conditional update protects the expected
            // progress state within this transaction.
            int updated = await _db.GameAttemptProgress
                .Where(x =>
                    x.AttemptId == command.AttemptId &&
                    x.GameId == command.GameId &&
                    x.Status ==
                        GameProgressStatus.WaitingForVerification &&
                    x.CurrentStepNumber == command.StepNumber &&
                    x.RejectedEvidenceCount ==
                        progress.RejectedEvidenceCount)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.CurrentStepNumber, nextStep)
                        .SetProperty(
                            x => x.RejectedEvidenceCount,
                            nextRejectionCount)
                        .SetProperty(
                            x => x.Status, nextStatus),
                    cancellationToken);

            if (updated != 1)
            {
                throw new DbUpdateConcurrencyException(
                    "Game progress changed during review.");
            }

            var decisionRow = new GameProofReviewDecisionRow
            {
                DecisionId = command.DecisionId,
                EvidenceId = command.EvidenceId,
                ReviewerUserId = reviewerId,
                IsApproved = command.Approved,
                Reason = reason,
                ReviewedAtUtc = reviewedAtUtc,
                CorrelationId = command.DecisionId
            };

            try
            {
                _db.GameProofReviewDecisions.Add(decisionRow);

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            finally
            {
                _db.Entry(decisionRow).State = EntityState.Detached;
            }

            return new GameStageProofDecisionResultDto(
                DecisionId: command.DecisionId,
                GameId: command.GameId,
                AttemptId: command.AttemptId,
                EvidenceId: command.EvidenceId,
                StepNumber: command.StepNumber,
                Approved: command.Approved,
                ReviewerUserId: reviewerId,
                Reason: reason,
                ReviewedAtUtc: reviewedAtUtc,
                CurrentStepNumber: nextStep,
                ProgressStatus: nextStatus,
                RejectedEvidenceCount: nextRejectionCount);
        });
    }

    private async Task<GameStageProofDecisionResultDto>
        RestoreDecisionResultAsync(
            GameProofReviewDecisionRow decision,
            GameProofEvidenceRow evidence,
            int totalSteps,
            Guid gameId,
            Guid attemptId,
            CancellationToken cancellationToken)
    {
        // Reconstruct the original transition snapshot
        // from immutable review and evidence history.
        var history = await (
            from review in _db.GameProofReviewDecisions.AsNoTracking()
            join proof in _db.GameProofEvidence.AsNoTracking()
                on review.EvidenceId equals proof.Id
            where proof.AttemptId == attemptId
            select new
            {
                review.DecisionId,
                review.IsApproved,
                proof.StepNumber,
                review.ReviewedAtUtc
            }).ToListAsync(cancellationToken);

        // Equal timestamps on different retakes cannot
        // establish an unambiguous rejection ordering.
        if (history.Any(x =>
            x.DecisionId != decision.DecisionId &&
            x.StepNumber == evidence.StepNumber &&
            x.ReviewedAtUtc == decision.ReviewedAtUtc))
        {
            throw new InvalidOperationException(
                "Original review snapshot cannot be " +
                "reconstructed unambiguously.");
        }

        if (decision.IsApproved &&
            evidence.StepNumber == totalSteps)
        {
            throw new InvalidOperationException(
                "Final-stage replay requires the dedicated " +
                "SQL completion workflow.");
        }

        int rejectedCount = history.Count(x =>
            !x.IsApproved &&
            (x.StepNumber < evidence.StepNumber ||
             (x.StepNumber == evidence.StepNumber &&
              (x.ReviewedAtUtc < decision.ReviewedAtUtc ||
               x.DecisionId == decision.DecisionId))));

        int resultStep = decision.IsApproved
            ? evidence.StepNumber + 1
            : evidence.StepNumber;

        var resultStatus = !decision.IsApproved &&
                           rejectedCount >= 3
            ? GameProgressStatus.Rejected
            : GameProgressStatus.InProgress;

        return new GameStageProofDecisionResultDto(
            DecisionId: decision.DecisionId,
            GameId: gameId,
            AttemptId: attemptId,
            EvidenceId: evidence.Id,
            StepNumber: evidence.StepNumber,
            Approved: decision.IsApproved,
            ReviewerUserId: decision.ReviewerUserId,
            Reason: decision.Reason,
            ReviewedAtUtc: decision.ReviewedAtUtc,
            CurrentStepNumber: resultStep,
            ProgressStatus: resultStatus,
            RejectedEvidenceCount: rejectedCount);
    }
}
