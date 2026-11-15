
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

/// <summary>
/// M6-MS03-T04
/// Grants 100 Coins to an eligible Admin-verified
/// Game winner exactly once per reward source.
///
/// Production integration requires an authoritative
/// M5 winner review and durable M6 ledger store.
/// </summary>
public sealed class GrantApprovedGameWinnerCoinsHandler
{
    private readonly IGameWinnerApprovedRewardReader _reader;
    private readonly ICoinRewardGrantStore _rewardStore;
    private readonly string _ruleVersion;

    public GrantApprovedGameWinnerCoinsHandler(
        IGameWinnerApprovedRewardReader reader,
        ICoinRewardGrantStore rewardStore,
        string ruleVersion)
    {
        _reader = reader ??
            throw new ArgumentNullException(nameof(reader));

        _rewardStore = rewardStore ??
            throw new ArgumentNullException(nameof(rewardStore));

        _ruleVersion =
            CoinLedgerVersionPolicy.RequireRuleVersion(
                ruleVersion);
    }

    public async Task<bool> HandleAsync(
        Guid gameId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(correlationId));
        }

        // 1. Read authoritative M5 approval details.
        var snapshot = await _reader.FindByGameIdAsync(
            gameId,
            cancellationToken);

        if (snapshot is null)
        {
            return false;
        }

        // Pending and rejected reviews earn zero.
        if (snapshot.ReviewOutcome !=
            GameWinnerReviewOutcome.Approved)
        {
            return false;
        }

        var game = snapshot.Game ??
            throw new InvalidOperationException(
                "Game record is missing.");

        var attempt = snapshot.Attempt ??
            throw new InvalidOperationException(
                "Game attempt record is missing.");

        // 2. Verify source relationships.
        if (game.Id != gameId ||
            attempt.GameId != gameId)
        {
            throw new InvalidOperationException(
                "Game and attempt reference mismatch.");
        }

        // 3. Previously verified rewards can be retried
        // after Game expiry. Fresh approvals after expiry
        // are rejected by timestamp validation below.
        if (game.Status != GameStatus.Published &&
            game.Status != GameStatus.Expired)
        {
            return false;
        }

        if (game.IsFlaggedForReview)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(game.WinnerUserId))
        {
            return false;
        }

        // 4. Creator cannot receive a winner reward.
        if (string.Equals(
            game.CreatorUserId,
            game.WinnerUserId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Game creator cannot win their own game.");
        }

        // 5. Verify the approved attempt's owner.
        if (!string.Equals(
            attempt.PlayerUserId,
            game.WinnerUserId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Winner and attempt player do not match.");
        }

        // 6. Require trusted Admin review references.
        if (string.IsNullOrWhiteSpace(
                snapshot.ApprovalRecordId) ||
            string.IsNullOrWhiteSpace(
                snapshot.ReviewerAdminUserId))
        {
            throw new InvalidOperationException(
                "Admin winner approval is required.");
        }

        // 7. Require authoritative Game timestamps.
        if (!game.SubmittedAtUtc.HasValue ||
            !game.ApprovedAtUtc.HasValue ||
            !game.ExpiresAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Game publication details are incomplete.");
        }

        var submittedAt = game.SubmittedAtUtc.Value;
        var publishedAt = game.ApprovedAtUtc.Value;
        var expiresAt = game.ExpiresAtUtc.Value;
        var verifiedAt = snapshot.VerifiedAtUtc;
        var startedAt = attempt.StartedAtUtc;

        if (submittedAt == default ||
            publishedAt == default ||
            expiresAt == default ||
            verifiedAt == default ||
            startedAt == default ||
            submittedAt.Offset != TimeSpan.Zero ||
            publishedAt.Offset != TimeSpan.Zero ||
            expiresAt.Offset != TimeSpan.Zero ||
            verifiedAt.Offset != TimeSpan.Zero ||
            startedAt.Offset != TimeSpan.Zero ||
            publishedAt < submittedAt ||
            expiresAt != publishedAt.AddDays(30) ||
            startedAt < publishedAt ||
            verifiedAt < startedAt ||
            verifiedAt >= expiresAt)
        {
            throw new InvalidOperationException(
                "Invalid Game winner eligibility timestamps.");
        }

        // 8. Reward amount comes from authoritative M6 rules.
        const CoinRewardType rewardType =
            CoinRewardType.GameWinnerApproved;

        int amount = CoinRewardRules.GetAmount(
            rewardType);

        // 9. Build the reward request.
        // GameId is a stable source key for retries.
        // GameAttemptId retains the verified attempt.
        var reward = new CoinRewardGrantRequest(
            EventId: Guid.NewGuid(),
            RewardType: rewardType,
            SourceRecordId: gameId.ToString("D"),
            RecipientUserId: game.WinnerUserId,
            CoinsAmount: amount,
            EligibilityConfirmedAtUtc: verifiedAt,
            CorrelationId: correlationId,
            RuleVersion: _ruleVersion,
            ApprovalRecordId:
                snapshot.ApprovalRecordId.Trim(),
            GameAttemptId: attempt.Id.ToString("D")
        );

        CoinRewardContractRules.ValidateGrant(reward);

        // 10. M6 store applies idempotent credit.
        // Production SQL must additionally enforce
        // a global one-winner-reward-per-Game constraint.
        return await _rewardStore.TryGrantOnceAsync(
            reward,
            cancellationToken);
    }
}
