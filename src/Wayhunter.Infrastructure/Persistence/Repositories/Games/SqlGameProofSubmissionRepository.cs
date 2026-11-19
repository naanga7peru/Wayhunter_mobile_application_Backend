
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.Games.Services;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5 SQL Game stage proof submission persistence.
///
/// Isolated testing implementation only.
/// Requires an externally provided trusted capture verifier.
///
/// The capture verifier's consumption and SQL commit are
/// not guaranteed to be atomic. Production integration
/// requires an approved consistency/recovery contract.
///
/// No shared migrations or production DI registration.
/// </summary>
public sealed class SqlGameProofSubmissionRepository
    : ISubmitGameStageProofStore
{
    private readonly WayhunterSharedDbContext _db;
    private readonly IGameProofCaptureVerifier _captureVerifier;

    public SqlGameProofSubmissionRepository(
        WayhunterSharedDbContext db,
        IGameProofCaptureVerifier captureVerifier)
    {
        _db = db ??
            throw new ArgumentNullException(nameof(db));

        _captureVerifier = captureVerifier ??
            throw new ArgumentNullException(nameof(captureVerifier));
    }

    public async Task<GameEvidence> SubmitCurrentStageProofAsync(
        SubmitGameStageProofCommand command,
        string playerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.GameId == Guid.Empty ||
            command.AttemptId == Guid.Empty ||
            command.StepNumber <= 0 ||
            string.IsNullOrWhiteSpace(command.MediaId) ||
            string.IsNullOrWhiteSpace(command.CaptureSessionId))
        {
            throw new ArgumentException(
                "Invalid Game stage proof request.",
                nameof(command));
        }

        if (string.IsNullOrWhiteSpace(playerUserId))
        {
            throw new ArgumentException(
                "Authenticated player ID is required.",
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
        string cleanMediaId = command.MediaId.Trim();
        string cleanSessionId = command.CaptureSessionId.Trim();

        if (cleanMediaId.Length > 256)
        {
            throw new ArgumentException(
                "Game proof media ID exceeds 256 characters.",
                nameof(command));
        }

        if (!_db.Database.IsSqlServer())
        {
            throw new InvalidOperationException(
                "Game proof submission requires SQL Server.");
        }

        // The capture verifier is a non-idempotent external
        // boundary. Never automatically replay the whole
        // transaction after consuming a capture.
        if (_db.Database.CreateExecutionStrategy()
                .RetriesOnFailure)
        {
            throw new InvalidOperationException(
                "Automatic SQL retries are not supported " +
                "for Game capture consumption.");
        }

        if (_db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "An existing database transaction is not supported.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Use the same Game-level lock as the SQL Attempt
        // and Proof Review repositories.
        var lockedGame = await _db.Games
            .FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Games] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {command.GameId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (lockedGame is null)
        {
            throw new KeyNotFoundException(
                "Game not found.");
        }

        var game = await new SqlGameRepository(_db)
            .GetByIdAsync(
                command.GameId,
                cancellationToken);

        if (game is null)
        {
            throw new KeyNotFoundException(
                "Game not found.");
        }

        GamePlayRules.EnsureCanStart(
            game,
            cleanPlayerId,
            nowUtc);

        if (!game.ApprovedAtUtc.HasValue ||
            game.ApprovedAtUtc.Value > nowUtc)
        {
            throw new InvalidOperationException(
                "Game approval is not effective.");
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

        if (!string.Equals(
                attempt.PlayerUserId,
                cleanPlayerId,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Game attempt belongs to another player.");
        }

        if (!attempt.DeadlineUtc.HasValue ||
            nowUtc < attempt.StartedAtUtc ||
            nowUtc >= attempt.DeadlineUtc.Value)
        {
            throw new InvalidOperationException(
                "Game attempt deadline is not active.");
        }

        var progress = await _db.GameAttemptProgress
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.AttemptId == command.AttemptId,
                cancellationToken);

        if (progress is null ||
            progress.GameId != command.GameId ||
            !string.Equals(
                progress.PlayerUserId,
                cleanPlayerId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Game attempt progress is invalid.");
        }

        // SQL progress is validated explicitly because
        // GameProgress has no persistence restoration API.
        if (progress.TotalSteps != game.DraftSteps.Count ||
            progress.Status != GameProgressStatus.InProgress ||
            progress.CurrentStepNumber != command.StepNumber ||
            !game.DraftSteps.Any(x =>
                x.GameId == command.GameId &&
                x.StepNumber == command.StepNumber))
        {
            throw new InvalidOperationException(
                "Game stage is not ready for proof submission.");
        }

        // Rejected evidence history is allowed.
        // A stage may not receive a second pending proof.
        bool pendingProofExists =
            await _db.GameProofEvidence
                .AsNoTracking()
                .AnyAsync(x =>
                    x.GameId == command.GameId &&
                    x.AttemptId == command.AttemptId &&
                    x.StepNumber == command.StepNumber &&
                    !_db.GameProofReviewDecisions.Any(d =>
                        d.EvidenceId == x.Id),
                    cancellationToken);

        if (pendingProofExists)
        {
            throw new InvalidOperationException(
                "Game stage already has pending proof.");
        }

        // Reject media IDs already persisted in another
        // proof. Cross-Game concurrent uniqueness still
        // needs an approved database/provider guarantee.
        bool mediaAlreadyUsed =
            await _db.GameProofEvidence
                .AsNoTracking()
                .AnyAsync(
                    x => x.MediaId == cleanMediaId,
                    cancellationToken);

        if (mediaAlreadyUsed)
        {
            throw new InvalidOperationException(
                "Game proof media has already been used.");
        }

        var evidence = new GameEvidence(
            Guid.NewGuid(),
            command.AttemptId,
            command.StepNumber,
            cleanMediaId,
            nowUtc);

        var evidenceRow = new GameProofEvidenceRow
        {
            Id = evidence.Id,
            GameId = command.GameId,
            AttemptId = evidence.AttemptId,
            StepNumber = evidence.StepNumber,
            MediaId = evidence.MediaId,
            SubmittedAtUtc = evidence.SubmittedAtUtc
        };

        try
        {
            // These writes remain uncommitted until trusted
            // capture verification succeeds.
            _db.GameProofEvidence.Add(evidenceRow);

            await _db.SaveChangesAsync(cancellationToken);

            int updated = await _db.GameAttemptProgress
                .Where(x =>
                    x.AttemptId == command.AttemptId &&
                    x.GameId == command.GameId &&
                    x.PlayerUserId == cleanPlayerId &&
                    x.Status == GameProgressStatus.InProgress &&
                    x.CurrentStepNumber == command.StepNumber &&
                    x.TotalSteps == progress.TotalSteps &&
                    x.RejectedEvidenceCount ==
                        progress.RejectedEvidenceCount)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.Status,
                        GameProgressStatus.WaitingForVerification),
                    cancellationToken);

            if (updated != 1)
            {
                throw new DbUpdateConcurrencyException(
                    "Game progress changed during submission.");
            }

            // Call the trusted verifier only after SQL
            // preconditions and SQL writes have succeeded.
            //
            // If verification fails, transaction disposal
            // rolls back the uncommitted SQL changes.
            //
            // IMPORTANT: A successful consumption followed
            // by SQL commit failure cannot automatically
            // be undone. No automatic retry is performed.
            bool verified =
                await _captureVerifier.VerifyAndConsumeAsync(
                    cleanSessionId,
                    cleanMediaId,
                    command.GameId,
                    command.AttemptId,
                    command.StepNumber,
                    cleanPlayerId,
                    nowUtc,
                    cancellationToken);

            if (!verified)
            {
                throw new UnauthorizedAccessException(
                    "Game camera proof verification failed.");
            }

            await transaction.CommitAsync(cancellationToken);

            return evidence;
        }
        finally
        {
            // Prevent stale tracked evidence after failure.
            _db.Entry(evidenceRow).State =
                EntityState.Detached;
        }
    }
}
