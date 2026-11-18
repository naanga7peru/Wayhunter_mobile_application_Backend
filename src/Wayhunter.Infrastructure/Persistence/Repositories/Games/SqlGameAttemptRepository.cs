
using System;
using System.Collections.Generic;
using System.Data;
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
/// M5 SQL Game Attempt and Progress persistence.
///
/// Uses the existing shared SQL Server schema.
/// Does not create or apply migrations.
///
/// Not registered as an active runtime store until
/// the remaining Game workflows are SQL-ready.
/// </summary>
public sealed class SqlGameAttemptRepository
    : IStartGameAttemptStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlGameAttemptRepository(
        WayhunterSharedDbContext db)
    {
        _db = db ??
            throw new ArgumentNullException(nameof(db));
    }

    public async Task<StartGameAttemptResultDto> StartOrResumeAsync(
        Guid gameId,
        string playerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        if (!_db.Database.IsSqlServer())
        {
            throw new InvalidOperationException(
                "Game Attempt SQL persistence requires SQL Server.");
        }

        string cleanPlayerId = playerUserId.Trim();

        // Execute user-managed transactions through the
        // configured EF Core execution strategy.
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // READ COMMITTED avoids broad serializable range locks.
            // The Game update lock serializes concurrent
            // Start requests for the same Game.
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            // Lock the parent Game before checking eligibility,
            // existing attempts, or creating a new attempt.
            var lockedGame = await _db.Games
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Games] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {gameId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (lockedGame is null)
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            // Reuse the SQL Game restoration logic:
            // Game, Steps, Hints and GameWinners.
            var gameRepository = new SqlGameRepository(_db);

            var game = await gameRepository.GetByIdAsync(
                gameId,
                cancellationToken);

            if (game is null)
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            // Reuse the existing domain eligibility rules.
            // Includes creator, status, flag, expiry
            // and winner-closed restrictions.
            GamePlayRules.EnsureCanStart(
                game,
                cleanPlayerId,
                nowUtc);

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

            cancellationToken.ThrowIfCancellationRequested();

            // Preserve the existing InMemory rule:
            // one tracked attempt per Game and Player.
            //
            // Even terminal attempts must not silently
            // receive a new timer or a new attempt.
            var existingAttempt = await _db.GameAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.GameId == gameId &&
                         x.PlayerUserId == cleanPlayerId,
                    cancellationToken);

            if (existingAttempt is not null)
            {
                var existingProgress =
                    await _db.GameAttemptProgress
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            x => x.AttemptId ==
                                 existingAttempt.Id,
                            cancellationToken);

                if (existingProgress is null ||
                    existingProgress.GameId != gameId ||
                    !string.Equals(
                        existingProgress.PlayerUserId,
                        cleanPlayerId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Existing Game attempt progress is invalid.");
                }

                if (!existingAttempt.DeadlineUtc.HasValue)
                {
                    throw new InvalidOperationException(
                        "Existing Game attempt deadline is missing.");
                }

                GameAttemptDeadlineRules.EnsureActive(
                    existingAttempt.StartedAtUtc,
                    existingAttempt.DeadlineUtc.Value,
                    nowUtc);

                if (existingProgress.Status !=
                        GameProgressStatus.InProgress &&
                    existingProgress.Status !=
                        GameProgressStatus.WaitingForVerification)
                {
                    throw new InvalidOperationException(
                        "Existing Game attempt is not active.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                await transaction.CommitAsync(
                    cancellationToken);

                return new StartGameAttemptResultDto(
                    AttemptId: existingAttempt.Id,
                    GameId: existingAttempt.GameId,
                    PlayerUserId: existingAttempt.PlayerUserId,
                    StartedAtUtc: existingAttempt.StartedAtUtc,
                    DeadlineUtc: existingAttempt.DeadlineUtc.Value,
                    CurrentStepNumber:
                        existingProgress.CurrentStepNumber,
                    ResumedExistingAttempt: true);
            }

            // New attempt: calculate the trusted deadline.
            // Never exceed the Game's original expiry.
            var deadlineUtc =
                GameAttemptDeadlineRules.CalculateDeadlineUtc(
                    nowUtc,
                    game.DurationMinutes.Value,
                    game.ExpiresAtUtc.Value);

            var attemptId = Guid.NewGuid();

            // Initialize progress through the domain model.
            var progress = new GameProgress(
                attemptId,
                game.DraftSteps.Count);

            progress.Start();

            var attemptRow = new GameAttemptRow
            {
                Id = attemptId,
                GameId = gameId,
                PlayerUserId = cleanPlayerId,
                StartedAtUtc = nowUtc,
                DeadlineUtc = deadlineUtc
            };

            var progressRow = new GameAttemptProgressRow
            {
                AttemptId = attemptId,
                GameId = gameId,
                PlayerUserId = cleanPlayerId,
                TotalSteps = progress.TotalSteps,
                CurrentStepNumber = progress.CurrentStepNumber,
                RejectedEvidenceCount =
                    progress.RejectedEvidenceCount,
                Status = progress.Status
            };

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Both inserts belong to one SQL transaction.
                // A failure must not leave a partial attempt.
                _db.GameAttempts.Add(attemptRow);
                _db.GameAttemptProgress.Add(progressRow);

                await _db.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);
            }
            finally
            {
                // Keep the DbContext clean if the execution
                // strategy must retry a failed transaction.
                _db.Entry(progressRow).State =
                    EntityState.Detached;

                _db.Entry(attemptRow).State =
                    EntityState.Detached;
            }

            return new StartGameAttemptResultDto(
                AttemptId: attemptId,
                GameId: gameId,
                PlayerUserId: cleanPlayerId,
                StartedAtUtc: nowUtc,
                DeadlineUtc: deadlineUtc,
                CurrentStepNumber: progress.CurrentStepNumber,
                ResumedExistingAttempt: false);
        });
    }
}
