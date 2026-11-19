using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

/// <summary>
/// Reconciles only committed M1/M4/M5 records not already represented in
/// the M6 outbox. It does not create or modify another module's approval.
/// </summary>
public sealed class SqlCommittedCoinRewardSource : ICommittedCoinRewardSource
{
    private readonly WayhunterSharedDbContext _db;
    private readonly string _ruleVersion;

    public SqlCommittedCoinRewardSource(
        WayhunterSharedDbContext db,
        string ruleVersion)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _ruleVersion = CoinLedgerVersionPolicy.RequireRuleVersion(ruleVersion);
    }

    public async Task<IReadOnlyList<CoinRewardGrantRequest>>
        FindCommittedRewardCandidatesAsync(
            int maxCount,
            CancellationToken cancellationToken = default)
    {
        if (maxCount < 1 || maxCount > 1000)
            throw new ArgumentOutOfRangeException(nameof(maxCount));

        var result = new List<CoinRewardGrantRequest>(maxCount);

        var accounts = await _db.Users.AsNoTracking()
            .Where(x => x.Status == UserAccountStatus.Active &&
                        x.VerifiedAtUtc != null &&
                        !_db.CoinRewardOutbox.Any(o =>
                            o.RewardType == CoinRewardType.WelcomeVerified &&
                            o.SourceRecordId == x.UserId))
            .OrderBy(x => x.VerifiedAtUtc)
            .Take(maxCount)
            .Select(x => new { x.UserId, VerifiedAt = x.VerifiedAtUtc!.Value })
            .ToListAsync(cancellationToken);
        foreach (var x in accounts)
        {
            Guid key = StableGuid($"welcome:{x.UserId}:{x.VerifiedAt:O}");
            result.Add(Build(
                key, CoinRewardType.WelcomeVerified, x.UserId, x.UserId,
                x.VerifiedAt, key));
        }

        int remaining = maxCount - result.Count;
        if (remaining > 0)
        {
            var hunters = await (
                from submission in _db.HunterSubmissions.AsNoTracking()
                join review in _db.HunterReviewDecisions.AsNoTracking()
                    on submission.Id equals review.HunterSubmissionId
                where submission.Status == HunterSubmissionStatus.Approved &&
                      review.ResultingStatus == HunterSubmissionStatus.Approved &&
                      !_db.CoinRewardOutbox.Any(o =>
                          o.RewardType == CoinRewardType.HunterApproved &&
                          o.SourceRecordId == submission.Id.ToString())
                orderby review.ReviewedAtUtc
                select new { submission.Id, submission.UserId, Review = review })
                .Take(remaining)
                .ToListAsync(cancellationToken);
            foreach (var x in hunters)
                result.Add(Build(
                    x.Review.Id, CoinRewardType.HunterApproved,
                    x.Id.ToString("D"), x.UserId,
                    x.Review.ReviewedAtUtc, x.Review.Id,
                    x.Review.Id.ToString("D")));
        }

        remaining = maxCount - result.Count;
        if (remaining > 0)
        {
            var creators = await (
                from approval in _db.GameCreationApprovals.AsNoTracking()
                join game in _db.Games.AsNoTracking()
                    on approval.GameId equals game.Id
                where !_db.CoinRewardOutbox.Any(o =>
                    o.RewardType == CoinRewardType.GameCreatorApproved &&
                    o.SourceRecordId == approval.GameId.ToString())
                orderby approval.ApprovedAtUtc
                select new { approval, game.CreatorUserId })
                .Take(remaining)
                .ToListAsync(cancellationToken);
            foreach (var x in creators)
                result.Add(new CoinRewardGrantRequest(
                    x.approval.RewardEventId,
                    CoinRewardType.GameCreatorApproved,
                    x.approval.GameId.ToString("D"),
                    x.CreatorUserId,
                    CoinRewardRules.GetAmount(CoinRewardType.GameCreatorApproved),
                    x.approval.ApprovedAtUtc,
                    x.approval.CorrelationId,
                    x.approval.RewardRuleVersion,
                    x.approval.ApprovalRecordId));
        }

        remaining = maxCount - result.Count;
        if (remaining > 0)
        {
            var winners = await (
                from decision in _db.GameResultDecisions.AsNoTracking()
                join winner in _db.GameWinners.AsNoTracking()
                    on new { decision.GameId, decision.AttemptId }
                    equals new { winner.GameId, winner.AttemptId }
                where decision.IsApproved &&
                      !_db.CoinRewardOutbox.Any(o =>
                          o.RewardType == CoinRewardType.GameWinnerApproved &&
                          o.SourceRecordId == decision.GameId.ToString())
                orderby decision.OccurredAtUtc
                select new { decision, winner.PlayerUserId })
                .Take(remaining)
                .ToListAsync(cancellationToken);
            foreach (var x in winners)
                result.Add(Build(
                    x.decision.DecisionId,
                    CoinRewardType.GameWinnerApproved,
                    x.decision.GameId.ToString("D"),
                    x.PlayerUserId,
                    x.decision.OccurredAtUtc,
                    x.decision.CorrelationId,
                    x.decision.DecisionId.ToString("D"),
                    x.decision.AttemptId.ToString("D")));
        }

        return result;
    }

    private CoinRewardGrantRequest Build(
        Guid eventId, CoinRewardType type, string sourceId,
        string recipientId, DateTimeOffset confirmedAtUtc,
        Guid correlationId, string? approvalId = null,
        string? attemptId = null) => new(
            eventId, type, sourceId, recipientId,
            CoinRewardRules.GetAmount(type), confirmedAtUtc,
            correlationId, _ruleVersion, approvalId, attemptId);

    private static Guid StableGuid(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}
