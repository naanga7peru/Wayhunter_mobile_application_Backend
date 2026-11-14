
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;

namespace Wayhunter.Application.Modules.CoinsWallet.Validators;

public static class CoinRewardContractRules
{
    public static string BuildIdempotencyKey(
        CoinRewardType rewardType,
        string sourceRecordId)
    {
        if (string.IsNullOrWhiteSpace(sourceRecordId))
            throw new ArgumentException(
                "Source record ID is required.",
                nameof(sourceRecordId));

        // Also validates that the reward type is supported.
        _ = CoinRewardRules.GetAmount(rewardType);

        string prefix = rewardType switch
        {
            CoinRewardType.WelcomeVerified => "welcome",
            CoinRewardType.HunterApproved => "hunter",
            CoinRewardType.GameCreatorApproved => "game-creator",
            CoinRewardType.GameWinnerApproved => "game-winner",
            _ => throw new ArgumentOutOfRangeException(
                nameof(rewardType))
        };

        return $"{prefix}:{sourceRecordId.Trim()}";
    }

    public static void ValidateGrant(
        CoinRewardGrantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EventId == Guid.Empty)
            throw new ArgumentException("Event ID required.");

        if (string.IsNullOrWhiteSpace(request.RecipientUserId))
            throw new ArgumentException("Recipient User ID required.");

        BuildIdempotencyKey(
            request.RewardType,
            request.SourceRecordId);

        int expectedAmount =
            CoinRewardRules.GetAmount(request.RewardType);

        if (request.CoinsAmount != expectedAmount)
            throw new ArgumentException(
                "Incorrect Coins reward amount.");

        if (request.EligibilityConfirmedAtUtc.Offset
            != TimeSpan.Zero)
            throw new ArgumentException(
                "Authoritative UTC timestamp required.");

        if (request.CorrelationId == Guid.Empty)
            throw new ArgumentException(
                "Correlation ID required.");

        if (string.IsNullOrWhiteSpace(request.RuleVersion))
            throw new ArgumentException(
                "Rule version required.");

        if (request.RewardType != CoinRewardType.WelcomeVerified
            && string.IsNullOrWhiteSpace(request.ApprovalRecordId))
            throw new ArgumentException(
                "Approval record required for reviewed rewards.");

        if (request.RewardType == CoinRewardType.GameWinnerApproved
            && string.IsNullOrWhiteSpace(request.GameAttemptId))
            throw new ArgumentException(
                "Verified GameAttemptId required.");
    }

    public static void ValidateCorrectionProposal(
        CoinRewardCorrectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OriginalRewardSourceId == Guid.Empty)
            throw new ArgumentException(
                "Original reward source required.");

        if (string.IsNullOrWhiteSpace(request.RequestedByAdminUserId))
            throw new ArgumentException(
                "Requesting Admin User ID required.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException(
                "Correction reason required.");

        if (request.CorrelationId == Guid.Empty)
            throw new ArgumentException(
                "Correlation ID required.");
    }
}
