using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>Read-only queries over the existing M5 SQL mappings.</summary>
public sealed class SqlGamesReadRepository :
    IAvailableGamesQueryStore,
    IPublicGameDetailsQueryStore,
    IMyGameAttemptsQueryStore,
    IMyGamesQueryStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlGamesReadRepository(WayhunterSharedDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<PublicGameSummaryDto>> GetAvailableGamesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureSqlServerAndUtc(nowUtc);

        return await _db.Games.AsNoTracking()
            .Where(game => game.Status == GameStatus.Published &&
                !game.IsFlaggedForReview &&
                game.SubmittedAtUtc.HasValue &&
                game.ApprovedAtUtc.HasValue &&
                game.ExpiresAtUtc.HasValue &&
                game.ApprovedAtUtc.Value <= nowUtc &&
                game.ExpiresAtUtc.Value > nowUtc &&
                game.Title != null && game.Title.Trim() != "" &&
                game.DurationMinutes > 0)
            .Where(game => !_db.GameWinners.Any(winner => winner.GameId == game.Id))
            .OrderBy(game => game.ApprovedAtUtc)
            .ThenBy(game => game.Id)
            .Select(game => new PublicGameSummaryDto(
                game.Id,
                game.Title!,
                game.Description,
                game.DurationMinutes!.Value,
                game.ApprovedAtUtc!.Value,
                game.ExpiresAtUtc!.Value))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PublicGameDetailsDto?> GetPublicGameAsync(
        Guid gameId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureSqlServerAndUtc(nowUtc);
        if (gameId == Guid.Empty)
            return null;

        return await _db.Games.AsNoTracking()
            .Where(game => game.Id == gameId &&
                game.Status == GameStatus.Published &&
                !game.IsFlaggedForReview &&
                game.SubmittedAtUtc.HasValue &&
                game.ApprovedAtUtc.HasValue &&
                game.ExpiresAtUtc.HasValue &&
                game.ApprovedAtUtc.Value <= nowUtc &&
                game.ExpiresAtUtc.Value > nowUtc &&
                game.Title != null && game.Title.Trim() != "" &&
                game.DurationMinutes > 0)
            .Where(game => !_db.GameWinners.Any(winner => winner.GameId == game.Id))
            .Select(game => new PublicGameDetailsDto(
                game.Id,
                game.Title!,
                game.Description,
                game.DurationMinutes!.Value,
                game.ApprovedAtUtc!.Value,
                game.ExpiresAtUtc!.Value,
                _db.GameSteps.Count(step => step.GameId == game.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MyGameAttemptDto>> GetMyAttemptsAsync(
        string playerUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureSqlServer();
        if (string.IsNullOrWhiteSpace(playerUserId))
            throw new ArgumentException("Player ID is required.", nameof(playerUserId));

        var attempts = await (
            from attempt in _db.GameAttempts.AsNoTracking()
            join progress in _db.GameAttemptProgress.AsNoTracking()
                on new { attempt.Id, attempt.GameId, attempt.PlayerUserId }
                equals new { Id = progress.AttemptId, progress.GameId, progress.PlayerUserId }
            where attempt.PlayerUserId == playerUserId
            orderby attempt.StartedAtUtc descending, attempt.Id
            select new MyGameAttemptDto(
                attempt.Id,
                attempt.GameId,
                attempt.StartedAtUtc,
                attempt.DeadlineUtc,
                progress.Status,
                progress.CurrentStepNumber,
                progress.TotalSteps))
            .ToArrayAsync(cancellationToken);

        return attempts;
    }

    public async Task<IReadOnlyList<MyGameDto>> GetMyGamesAsync(
        string creatorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureSqlServer();
        if (!Guid.TryParseExact(creatorUserId, "D", out var creatorId) || creatorId == Guid.Empty)
            return Array.Empty<MyGameDto>();
        return await _db.Games.AsNoTracking()
            .Where(game => game.CreatorUserId == creatorId.ToString("D"))
            .OrderByDescending(game => game.ApprovedAtUtc)
            .ThenBy(game => game.Id)
            .Select(game => new MyGameDto(
                game.Id, game.Title, game.Status, game.ApprovedAtUtc, game.ExpiresAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private void EnsureSqlServerAndUtc(DateTimeOffset nowUtc)
    {
        EnsureSqlServer();
        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Valid UTC timestamp is required.", nameof(nowUtc));
    }

    private void EnsureSqlServer()
    {
        if (!_db.Database.IsSqlServer())
            throw new InvalidOperationException("Games read persistence requires SQL Server.");
    }
}
