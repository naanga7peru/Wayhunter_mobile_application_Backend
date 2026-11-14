
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

/// <summary>
/// M6-MS03-T02: Rewards an Admin-approved Hunter submission.
///
/// Requires trusted, committed M4 approval data.
/// Must only be called by the backend workflow.
/// </summary>
public sealed class GrantApprovedHunterCoinsHandler
{
    private readonly IHunterApprovedRewardReader _reader;
    private readonly ICoinRewardGrantStore _rewardStore;
    private readonly string _ruleVersion;

    public GrantApprovedHunterCoinsHandler(
        IHunterApprovedRewardReader reader,
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
        Guid submissionId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission ID is required.",
                nameof(submissionId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(correlationId));
        }

        // 1. Load committed M4 submission/review state.
        var snapshot = await _reader.FindBySubmissionIdAsync(
            submissionId,
            cancellationToken);

        if (snapshot is null)
            return false;

        // 2. Reject incorrect submission/review links.
        if (snapshot.SubmissionId != submissionId ||
            snapshot.ReviewSubmissionId != submissionId)
        {
            throw new InvalidOperationException(
                "Hunter submission/review reference mismatch.");
        }

        // 3. Only confirmed Admin approval is eligible.
        // Pending, Held and Rejected all earn zero.
        if (snapshot.SubmissionStatus !=
                HunterSubmissionStatus.Approved ||
            snapshot.ReviewStatus !=
                HunterSubmissionStatus.Approved)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                snapshot.HunterUserId))
        {
            throw new InvalidOperationException(
                "Hunter owner is required.");
        }

        if (snapshot.ReviewId == Guid.Empty ||
            string.IsNullOrWhiteSpace(
                snapshot.ReviewerAdminUserId))
        {
            throw new InvalidOperationException(
                "Trusted Admin approval is required.");
        }

        // 4. Verify trusted UTC timestamps.
        if (!snapshot.SubmittedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Hunter submission time is required.");
        }

        var submittedAt = snapshot.SubmittedAtUtc.Value;
        var approvedAt = snapshot.ReviewedAtUtc;

        if (submittedAt == default ||
            submittedAt.Offset != TimeSpan.Zero ||
            approvedAt == default ||
            approvedAt.Offset != TimeSpan.Zero ||
            approvedAt < submittedAt)
        {
            throw new InvalidOperationException(
                "Invalid Hunter approval timestamps.");
        }

        // 5. Reward amount is controlled by M6.
        const CoinRewardType rewardType =
            CoinRewardType.HunterApproved;

        int amount = CoinRewardRules.GetAmount(
            rewardType);

        // 6. SubmissionId is the stable reward source.
        // Even if EventId or ReviewId changes on retry,
        // this approved submission must earn only once.
        var reward = new CoinRewardGrantRequest(
            EventId: Guid.NewGuid(),
            RewardType: rewardType,
            SourceRecordId: submissionId.ToString("D"),
            RecipientUserId: snapshot.HunterUserId.Trim(),
            CoinsAmount: amount,
            EligibilityConfirmedAtUtc: approvedAt,
            CorrelationId: correlationId,
            RuleVersion: _ruleVersion,
            ApprovalRecordId: snapshot.ReviewId.ToString("D")
        );

        CoinRewardContractRules.ValidateGrant(reward);

        // The authoritative store must atomically enforce
        // Recipient + RewardType + SubmissionId uniqueness.
        return await _rewardStore.TryGrantOnceAsync(
            reward,
            cancellationToken);
    }
}
