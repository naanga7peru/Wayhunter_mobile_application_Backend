
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
            RuleVersion: request.RuleVersion.Trim())
        {
            RewardEventId = request.EventId
        };
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

    internal static void ValidateConfirmed(
        CoinRewardConfirmedEvent rewardEvent)
    {
        ArgumentNullException.ThrowIfNull(rewardEvent);
        if (rewardEvent.State != CoinRewardConfirmationState.Confirmed ||
            rewardEvent.LedgerEntryId == Guid.Empty ||
            string.IsNullOrWhiteSpace(rewardEvent.RecipientUserId) ||
            rewardEvent.ConfirmedAtUtc == default ||
            rewardEvent.ConfirmedAtUtc.Offset != TimeSpan.Zero ||
            rewardEvent.CorrelationId == Guid.Empty)
            throw new InvalidOperationException(
                "Unconfirmed or invalid ledger event.");

        var (module, category) = MapSource(rewardEvent.RewardType);
        if (rewardEvent.SourceModule != module ||
            rewardEvent.SourceCategory != category ||
            rewardEvent.CoinsAmount != CoinRewardRules.GetAmount(rewardEvent.RewardType) ||
            rewardEvent.DeliveryKey != CoinRewardContractRules.BuildIdempotencyKey(
                rewardEvent.RewardType, rewardEvent.SourceRecordId) ||
            string.IsNullOrWhiteSpace(rewardEvent.RuleVersion))
            throw new InvalidOperationException("Invalid confirmed reward payload.");
    }
}

public sealed record CoinRewardConsumerPublishResult(
    bool NotificationPublished,
    bool RankingPublished)
{
    public bool ProfilePublished { get; init; }
}

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

    private readonly ICoinRewardProfileConsumer? _profile;

    public PublishConfirmedCoinRewardService(
        ICoinRewardNotificationConsumer notifications,
        ICoinRewardRankingConsumer ranking,
        ICoinRewardProfileConsumer? profile = null)
    {
        _notifications = notifications ??
            throw new ArgumentNullException(
                nameof(notifications));

        _ranking = ranking ??
            throw new ArgumentNullException(
                nameof(ranking));
        _profile = profile;
    }

    public async Task<CoinRewardConsumerPublishResult>
        PublishAsync(
            CoinRewardConfirmedEvent rewardEvent,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CoinRewardConfirmedEventFactory.ValidateConfirmed(rewardEvent);

        // Each consumer independently enforces
        // durable idempotency by DeliveryKey.
        bool notified = await _notifications.PublishOnceAsync(
            rewardEvent,
            cancellationToken);

        bool ranked = await _ranking.PublishOnceAsync(
            rewardEvent,
            cancellationToken);

        bool profiled = _profile is not null &&
            await _profile.PublishOnceAsync(
                rewardEvent,
                cancellationToken);

        return new CoinRewardConsumerPublishResult(
            notified,
            ranked)
        {
            ProfilePublished = profiled
        };
    }
}
