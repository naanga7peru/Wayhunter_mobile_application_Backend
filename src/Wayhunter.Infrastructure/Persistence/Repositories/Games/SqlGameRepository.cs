
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed class SqlGameRepository
    : IGameDraftDeletionRepository, IExpiredGameArchiveStore
{
    private const int RowVersionLength = 8;

    private readonly WayhunterSharedDbContext _db;

    // Remember the RowVersion belonging to each loaded Game.
    // This repository must be scoped to a single unit of work.
    private readonly Dictionary<Game, byte[]> _versions =
        new(ReferenceEqualityComparer.Instance);

    public SqlGameRepository(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Game?> GetByIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException(
                "Game ID is required.", nameof(gameId));

        var row = await _db.Games
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == gameId,
                cancellationToken);

        if (row is null)
            return null;

        var steps = await _db.GameSteps
            .AsNoTracking()
            .Where(x => x.GameId == gameId)
            .OrderBy(x => x.StepNumber)
            .ToListAsync(cancellationToken);

        var stepIds = steps.Select(x => x.Id).ToArray();

        var hints = await _db.GameHints
            .AsNoTracking()
            .Where(x => stepIds.Contains(x.GameStepId))
            .ToListAsync(cancellationToken);

        var winnerUserId = await _db.GameWinners
            .AsNoTracking()
            .Where(x => x.GameId == gameId)
            .Select(x => x.PlayerUserId)
            .SingleOrDefaultAsync(cancellationToken);

        var game = Game.RestoreFromPersistence(
            id: row.Id,
            creatorUserId: row.CreatorUserId,
            title: row.Title,
            description: row.Description,
            durationMinutes: row.DurationMinutes,
            status: row.Status,
            submittedAtUtc: row.SubmittedAtUtc,
            approvedAtUtc: row.ApprovedAtUtc,
            expiresAtUtc: row.ExpiresAtUtc,
            winnerUserId: winnerUserId,
            isFlaggedForReview: row.IsFlaggedForReview,
            flagReason: row.FlagReason,
            rejectionReason: row.RejectionReason,
            disabledReason: row.DisabledReason,
            lastModeratedAtUtc: row.LastModeratedAtUtc,
            steps: steps.Select(x => new GameStep(
                x.Id, x.GameId, x.StepNumber)).ToArray(),
            hints: hints.Select(x => new GameHint(
                x.Id,
                x.GameStepId,
                x.TextHint,
                x.PhotoMediaId)).ToArray());

        RememberVersion(game, row.RowVersion);

        return game;
    }

    public async Task AddAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.WinnerUserId is not null)
            throw new InvalidOperationException(
                "Winner records require the dedicated approval workflow.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);

        var row = new GameRow { Id = game.Id };
        CopyGameProperties(game, row);

        await _db.Games.AddAsync(row, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _db.GameSteps.AddRange(
            game.DraftSteps.Select(x => new GameStepRow
            {
                Id = x.Id,
                GameId = x.GameId,
                StepNumber = x.StepNumber
            }));

        await _db.SaveChangesAsync(cancellationToken);

        _db.GameHints.AddRange(
            game.DraftHints.Select(x => new GameHintRow
            {
                Id = x.Id,
                GameStepId = x.GameStepId,
                TextHint = x.TextHint,
                PhotoMediaId = x.PhotoMediaId
            }));

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        RememberVersion(game, row.RowVersion);
    }

    public async Task UpdateAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (!_versions.TryGetValue(game, out var expectedVersion))
            throw new InvalidOperationException(
                "Game must be loaded by this repository before updating.");

        if (expectedVersion.Length != RowVersionLength)
            throw new InvalidOperationException(
                "A valid SQL Server RowVersion is required.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);

        // =====================================
        // SQL SERVER CONCURRENCY LOCK
        // =====================================
        // UPDLOCK:
        // Acquire an update lock before child-row modifications.
        //
        // HOLDLOCK:
        // Keep the lock until the transaction completes.
        //
        // This serializes competing updates to the same Game
        // when those updates follow this locking protocol.
        //
        // RowVersion is still checked below.

        var row = await _db.Games
            .FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Games] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {game.Id}")
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
            throw new KeyNotFoundException("Game not found.");

        if (!string.Equals(
                row.CreatorUserId,
                game.CreatorUserId,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Game creator cannot be changed.");

        if (!row.RowVersion.AsSpan().SequenceEqual(expectedVersion))
            throw new DbUpdateConcurrencyException(
                "Game was modified by another operation.");

        var currentWinner = await _db.GameWinners
            .AsNoTracking()
            .Where(x => x.GameId == game.Id)
            .Select(x => x.PlayerUserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!string.Equals(
                currentWinner,
                game.WinnerUserId,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Winner changes require the dedicated approval workflow.");

        var existingSteps = await _db.GameSteps
            .Where(x => x.GameId == game.Id)
            .ToListAsync(cancellationToken);

        var existingStepIds =
            existingSteps.Select(x => x.Id).ToArray();

        var existingHints = await _db.GameHints
            .Where(x => existingStepIds.Contains(x.GameStepId))
            .ToListAsync(cancellationToken);

        var stepsChanged =
            existingSteps.Count != game.DraftSteps.Count ||
            existingSteps.Any(old =>
                !game.DraftSteps.Any(current =>
                    current.Id == old.Id &&
                    current.GameId == old.GameId &&
                    current.StepNumber == old.StepNumber));

        var hintsChanged =
            existingHints.Count != game.DraftHints.Count ||
            existingHints.Any(old =>
                !game.DraftHints.Any(current =>
                    current.Id == old.Id &&
                    current.GameStepId == old.GameStepId &&
                    current.TextHint == old.TextHint &&
                    current.PhotoMediaId == old.PhotoMediaId));

        if (stepsChanged || hintsChanged)
        {
            if (row.Status != GameStatus.Draft)
                throw new InvalidOperationException(
                    "Only draft games can replace stages or hints.");

            // Delete child records first because all FKs
            // use DeleteBehavior.NoAction.
            _db.GameHints.RemoveRange(existingHints);
            await _db.SaveChangesAsync(cancellationToken);

            _db.GameSteps.RemoveRange(existingSteps);
            await _db.SaveChangesAsync(cancellationToken);

            _db.GameSteps.AddRange(
                game.DraftSteps.Select(x => new GameStepRow
                {
                    Id = x.Id,
                    GameId = x.GameId,
                    StepNumber = x.StepNumber
                }));

            await _db.SaveChangesAsync(cancellationToken);

            _db.GameHints.AddRange(
                game.DraftHints.Select(x => new GameHintRow
                {
                    Id = x.Id,
                    GameStepId = x.GameStepId,
                    TextHint = x.TextHint,
                    PhotoMediaId = x.PhotoMediaId
                }));

            await _db.SaveChangesAsync(cancellationToken);
        }

        CopyGameProperties(game, row);

        var entry = _db.Entry(row);

        // Use the version captured when the domain Game
        // was originally loaded, not a freshly fetched token.
        entry.Property(x => x.RowVersion).OriginalValue =
            expectedVersion.ToArray();

        entry.Property(x => x.RowVersion).IsModified = false;

        // Force an UPDATE even if only child records changed,
        // so SQL Server checks the original RowVersion.
        entry.Property(x => x.Status).IsModified = true;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        RememberVersion(game, row.RowVersion);
    }

    public async Task DeleteAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException(
                "Game ID is required.", nameof(gameId));

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);

        var row = await _db.Games
            .SingleOrDefaultAsync(
                x => x.Id == gameId,
                cancellationToken);

        if (row is null)
            throw new KeyNotFoundException("Game not found.");

        if (row.Status != GameStatus.Draft)
            throw new InvalidOperationException(
                "Only draft games can be deleted.");

        var steps = await _db.GameSteps
            .Where(x => x.GameId == gameId)
            .ToListAsync(cancellationToken);

        var stepIds = steps.Select(x => x.Id).ToArray();

        var hints = await _db.GameHints
            .Where(x => stepIds.Contains(x.GameStepId))
            .ToListAsync(cancellationToken);

        _db.GameHints.RemoveRange(hints);
        await _db.SaveChangesAsync(cancellationToken);

        _db.GameSteps.RemoveRange(steps);
        await _db.SaveChangesAsync(cancellationToken);

        // SQL Server RowVersion protects this deletion.
        // Other foreign keys can still reject unsafe deletion.
        _db.Games.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> ArchiveExpiredGamesAsync(
        DateTimeOffset nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "A valid UTC timestamp is required.", nameof(nowUtc));

        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount));

        var expiredIds = await _db.Games
            .AsNoTracking()
            .Where(row => row.Status == GameStatus.Published
                && row.ExpiresAtUtc.HasValue
                && row.ExpiresAtUtc.Value <= nowUtc)
            .OrderBy(row => row.ExpiresAtUtc)
            .ThenBy(row => row.Id)
            .Select(row => row.Id)
            .Take(maxCount)
            .ToArrayAsync(cancellationToken);

        var archivedCount = 0;
        foreach (var gameId in expiredIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var game = await GetByIdAsync(gameId, cancellationToken);
            if (game is null || !game.ArchiveIfExpired(nowUtc))
                continue;

            await UpdateAsync(game, cancellationToken);
            archivedCount++;
        }

        return archivedCount;
    }

    private static void CopyGameProperties(
        Game game,
        GameRow row)
    {
        row.CreatorUserId = game.CreatorUserId;
        row.Title = game.Title;
        row.Description = game.Description;
        row.DurationMinutes = game.DurationMinutes;
        row.Status = game.Status;
        row.SubmittedAtUtc = game.SubmittedAtUtc;
        row.ApprovedAtUtc = game.ApprovedAtUtc;
        row.ExpiresAtUtc = game.ExpiresAtUtc;
        row.IsFlaggedForReview = game.IsFlaggedForReview;
        row.FlagReason = game.FlagReason;
        row.RejectionReason = game.RejectionReason;
        row.DisabledReason = game.DisabledReason;
        row.LastModeratedAtUtc = game.LastModeratedAtUtc;
    }

    private void RememberVersion(Game game, byte[] rowVersion)
    {
        if (rowVersion.Length != RowVersionLength)
            throw new InvalidOperationException(
                "SQL Server must provide an 8-byte RowVersion.");

        _versions[game] = rowVersion.ToArray();
    }
}
