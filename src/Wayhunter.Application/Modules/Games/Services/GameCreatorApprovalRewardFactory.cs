
using System;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Services;

public static class GameCreatorApprovalRewardFactory
{
    // =====================================
    // BUILD CREATOR APPROVAL REWARD EVENT
    // M5-MS03-T04
    // =====================================

    public static CoinRewardGrantRequest Create(
        Game game,
        string approvalRecordId,
        Guid eventId,
        Guid correlationId,
        string ruleVersion)
    {
        ArgumentNullException.ThrowIfNull(game);

        // Only published games can produce
        // creator approval reward events.
        if (game.Status != GameStatus.Published)
        {
            throw new InvalidOperationException(
                "Game must be published.");
        }

        // A real draft submission must exist.
        // Reject the legacy pending-game shortcut.
        if (!game.SubmittedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Game submission is required.");
        }

        // Admin approval must be recorded.
        if (!game.ApprovedAtUtc.HasValue ||
            !game.ExpiresAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Admin approval details are required.");
        }

        var approvedAtUtc = game.ApprovedAtUtc.Value;

        if (approvedAtUtc.Offset != TimeSpan.Zero ||
            approvedAtUtc < game.SubmittedAtUtc.Value)
        {
            throw new InvalidOperationException(
                "Invalid approval timestamp.");
        }

        // Confirm the 30-day publication rule.
        if (game.ExpiresAtUtc.Value !=
            approvedAtUtc.AddDays(30))
        {
            throw new InvalidOperationException(
                "Invalid game expiry timestamp.");
        }

        if (string.IsNullOrWhiteSpace(approvalRecordId))
        {
            throw new ArgumentException(
                "Approval record ID is required.",
                nameof(approvalRecordId));
        }

        if (string.IsNullOrWhiteSpace(ruleVersion))
        {
            throw new ArgumentException(
                "Reward rule version is required.",
                nameof(ruleVersion));
        }

        // Use the authoritative M6 reward type.
        const CoinRewardType rewardType =
            CoinRewardType.GameCreatorApproved;

        // M6 defines this amount as 100 Coins.
        var coinsAmount =
            CoinRewardRules.GetAmount(rewardType);

        // GameId is the stable reward source.
        // M6 must enforce uniqueness for this
        // source and reward type in its ledger.
        var rewardEvent = new CoinRewardGrantRequest(
            EventId: eventId,
            RewardType: rewardType,
            SourceRecordId: game.Id.ToString("D"),
            RecipientUserId: game.CreatorUserId,
            CoinsAmount: coinsAmount,
            EligibilityConfirmedAtUtc: approvedAtUtc,
            CorrelationId: correlationId,
            RuleVersion: ruleVersion.Trim(),
            ApprovalRecordId: approvalRecordId.Trim()
        );

        // Reuse M6 validation rather than
        // creating separate Games reward rules.
        CoinRewardContractRules.ValidateGrant(
            rewardEvent);

        return rewardEvent;
    }
}
