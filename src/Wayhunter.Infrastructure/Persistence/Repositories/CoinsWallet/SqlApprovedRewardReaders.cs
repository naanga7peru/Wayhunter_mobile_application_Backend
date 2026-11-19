using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlHunterApprovedRewardReader : IHunterApprovedRewardReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlHunterApprovedRewardReader(WayhunterSharedDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<HunterApprovedRewardSnapshot?> FindBySubmissionIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var row = await (
            from submission in _db.HunterSubmissions.AsNoTracking()
            join review in _db.HunterReviewDecisions.AsNoTracking()
                on submission.Id equals review.HunterSubmissionId
            where submission.Id == submissionId &&
                  submission.Status == HunterSubmissionStatus.Approved &&
                  review.ResultingStatus == HunterSubmissionStatus.Approved
            orderby review.ReviewedAtUtc descending
            select new
            {
                Submission = submission,
                Review = review
            }).FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : new HunterApprovedRewardSnapshot(
            row.Submission.Id,
            row.Submission.UserId,
            row.Submission.Status,
            row.Submission.SubmittedAtUtc,
            row.Review.Id,
            row.Review.HunterSubmissionId,
            row.Review.ResultingStatus,
            row.Review.ReviewerUserId,
            row.Review.ReviewedAtUtc);
    }
}

public sealed class SqlGameApprovedRewardReader :
    IGameCreatorApprovedRewardReader,
    IGameWinnerApprovedRewardReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlGameApprovedRewardReader(WayhunterSharedDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    async Task<GameCreatorApprovedRewardSnapshot?>
        IGameCreatorApprovedRewardReader.FindByGameIdAsync(
            Guid gameId,
            CancellationToken cancellationToken)
    {
        var approval = await _db.GameCreationApprovals
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.GameId == gameId, cancellationToken);

        if (approval is null)
            return null;

        var game = await RestoreGameAsync(gameId, cancellationToken);
        return game is null
            ? null
            : new GameCreatorApprovedRewardSnapshot(
                game,
                approval.ApprovalRecordId);
    }

    async Task<GameWinnerApprovedRewardSnapshot?>
        IGameWinnerApprovedRewardReader.FindByGameIdAsync(
            Guid gameId,
            CancellationToken cancellationToken)
    {
        var row = await (
            from decision in _db.GameResultDecisions.AsNoTracking()
            join winner in _db.GameWinners.AsNoTracking()
                on new { decision.GameId, decision.AttemptId }
                equals new { winner.GameId, winner.AttemptId }
            join attemptRow in _db.GameAttempts.AsNoTracking()
                on new { decision.GameId, decision.AttemptId }
                equals new { attemptRow.GameId, AttemptId = attemptRow.Id }
            where decision.GameId == gameId && decision.IsApproved
            select new
            {
                Decision = decision,
                Winner = winner,
                Attempt = attemptRow
            }).SingleOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var game = await RestoreGameAsync(gameId, cancellationToken);
        if (game is null)
            return null;

        var attempt = new GameAttempt(
            row.Attempt.Id,
            row.Attempt.GameId,
            row.Attempt.PlayerUserId,
            row.Attempt.StartedAtUtc);

        return new GameWinnerApprovedRewardSnapshot(
            game,
            attempt,
            GameWinnerReviewOutcome.Approved,
            row.Decision.DecisionId.ToString("D"),
            row.Decision.AdminUserId,
            row.Decision.OccurredAtUtc);
    }

    private async Task<Game?> RestoreGameAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var row = await _db.Games.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == gameId, cancellationToken);
        if (row is null)
            return null;

        var winner = await _db.GameWinners.AsNoTracking()
            .Where(x => x.GameId == gameId)
            .Select(x => x.PlayerUserId)
            .SingleOrDefaultAsync(cancellationToken);
        var steps = await _db.GameSteps.AsNoTracking()
            .Where(x => x.GameId == gameId)
            .OrderBy(x => x.StepNumber)
            .Select(x => new GameStep(x.Id, x.GameId, x.StepNumber))
            .ToListAsync(cancellationToken);
        var stepIds = steps.Select(x => x.Id).ToArray();
        var hints = await _db.GameHints.AsNoTracking()
            .Where(x => stepIds.Contains(x.GameStepId))
            .Select(x => new GameHint(
                x.Id, x.GameStepId, x.TextHint, x.PhotoMediaId))
            .ToListAsync(cancellationToken);

        return Game.RestoreFromPersistence(
            row.Id, row.CreatorUserId, row.Title, row.Description,
            row.DurationMinutes, row.Status, row.SubmittedAtUtc,
            row.ApprovedAtUtc, row.ExpiresAtUtc, winner,
            row.IsFlaggedForReview, row.FlagReason, row.RejectionReason,
            row.DisabledReason, row.LastModeratedAtUtc, steps, hints);
    }
}
