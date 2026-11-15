
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

public static class CoinRewardConfirmedEventFactory
{
    // Call only after a trusted ledger entry has committed.
    public static CoinRewardConfirmedEvent CreateFromCommittedLedger(
        CoinRewardGrantRequest request,
        Guid ledgerEntryId,
        DateTimeOffset confirmedAtUtc)
    {
        CoinRewardContractRules.ValidateGrant(request);

        if (ledgerEntryId == Guid.Empty)
            throw new ArgumentException(
                "Committed ledger entry ID required.",
                nameof(ledgerEntryId));

        if (confirmedAtUtc == default ||
            confirmedAtUtc.Offset != TimeSpan.Zero ||
            confirmedAtUtc <
                request.EligibilityConfirmedAtUtc)
        {
            throw new ArgumentException(
                "Invalid confirmed UTC timestamp.",
                nameof(confirmedAtUtc));
        }

        var (module, category) = MapSource(
            request.RewardType);

        return new CoinRewardConfirmedEvent(
            LedgerEntryId: ledgerEntryId,
            DeliveryKey:
                CoinRewardContractRules.BuildIdempotencyKey(
                    request.RewardType,
                    request.SourceRecordId),
            RecipientUserId: request.RecipientUserId.Trim(),
            RewardType: request.RewardType,
            SourceModule: module,
            SourceCategory: category,
            SourceRecordId: request.SourceRecordId.Trim(),
            CoinsAmount: request.CoinsAmount,
            ConfirmedAtUtc: confirmedAtUtc,
            State: CoinRewardConfirmationState.Confirmed,
            CorrelationId: request.CorrelationId,
            RuleVersion: request.RuleVersion.Trim());
    }

    internal static (
        CoinRewardSourceModule Module,
        string Category) MapSource(
            CoinRewardType rewardType)
    {
        return rewardType switch
        {
            CoinRewardType.WelcomeVerified =>
                (CoinRewardSourceModule.IdentitySocial,
                 "verified-account"),

            CoinRewardType.HunterApproved =>
                (CoinRewardSourceModule.HunterPlaces,
                 "approved-place"),

            CoinRewardType.GameCreatorApproved =>
                (CoinRewardSourceModule.Games,
                 "approved-game-creator"),

            CoinRewardType.GameWinnerApproved =>
                (CoinRewardSourceModule.Games,
                 "approved-game-winner"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(rewardType))
        };
    }
}

public sealed record CoinRewardConsumerPublishResult(
    bool NotificationPublished,
    bool RankingPublished);

/// <summary>
/// T06: Dispatch a committed Coins reward to
/// independent notification and ranking consumers.
///
/// This service is NOT a background worker.
/// Durable delivery/retry integration is pending.
/// </summary>
public sealed class PublishConfirmedCoinRewardService
{
    private readonly ICoinRewardNotificationConsumer
        _notifications;

    private readonly ICoinRewardRankingConsumer
        _ranking;

    public PublishConfirmedCoinRewardService(
        ICoinRewardNotificationConsumer notifications,
        ICoinRewardRankingConsumer ranking)
    {
        _notifications = notifications ??
            throw new ArgumentNullException(
                nameof(notifications));

        _ranking = ranking ??
            throw new ArgumentNullException(
                nameof(ranking));
    }

    public async Task<CoinRewardConsumerPublishResult>
        PublishAsync(
            CoinRewardConfirmedEvent rewardEvent,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(rewardEvent);

        if (rewardEvent.State !=
            CoinRewardConfirmationState.Confirmed ||
            rewardEvent.LedgerEntryId == Guid.Empty ||
            string.IsNullOrWhiteSpace(
                rewardEvent.RecipientUserId) ||
            rewardEvent.ConfirmedAtUtc == default ||
            rewardEvent.ConfirmedAtUtc.Offset != TimeSpan.Zero ||
            rewardEvent.CorrelationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Unconfirmed or invalid ledger event.");
        }

        var (expectedModule, expectedCategory) =
            CoinRewardConfirmedEventFactory.MapSource(
                rewardEvent.RewardType);

        if (rewardEvent.SourceModule != expectedModule ||
            rewardEvent.SourceCategory != expectedCategory ||
            rewardEvent.CoinsAmount !=
                CoinRewardRules.GetAmount(
                    rewardEvent.RewardType) ||
            rewardEvent.DeliveryKey !=
                CoinRewardContractRules.BuildIdempotencyKey(
                    rewardEvent.RewardType,
                    rewardEvent.SourceRecordId) ||
            string.IsNullOrWhiteSpace(
                rewardEvent.RuleVersion))
        {
            throw new InvalidOperationException(
                "Invalid confirmed reward payload.");
        }

        // Each consumer independently enforces
        // durable idempotency by DeliveryKey.
        bool notified = await _notifications.PublishOnceAsync(
            rewardEvent,
            cancellationToken);

        bool ranked = await _ranking.PublishOnceAsync(
            rewardEvent,
            cancellationToken);

        return new CoinRewardConsumerPublishResult(
            notified,
            ranked);
    }
}
