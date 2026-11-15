
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

public enum CoinRewardSourceModule
{
    IdentitySocial = 1,
    HunterPlaces = 2,
    Games = 3
}

public enum CoinRewardConfirmationState
{
    Confirmed = 1
}

/// <summary>
/// A reward event backed by a committed ledger entry.
/// Never create this from a mobile-supplied approval flag.
/// </summary>
public sealed record CoinRewardConfirmedEvent(
    Guid LedgerEntryId,
    string DeliveryKey,
    string RecipientUserId,
    CoinRewardType RewardType,
    CoinRewardSourceModule SourceModule,
    string SourceCategory,
    string SourceRecordId,
    int CoinsAmount,
    DateTimeOffset ConfirmedAtUtc,
    CoinRewardConfirmationState State,
    Guid CorrelationId,
    string RuleVersion
);

/// <summary>
/// Implementations must deduplicate by DeliveryKey
/// across process restarts and concurrent workers.
/// </summary>
public interface ICoinRewardNotificationConsumer
{
    Task<bool> PublishOnceAsync(
        CoinRewardConfirmedEvent rewardEvent,
        CancellationToken cancellationToken = default);
}

public interface ICoinRewardRankingConsumer
{
    Task<bool> PublishOnceAsync(
        CoinRewardConfirmedEvent rewardEvent,
        CancellationToken cancellationToken = default);
}
