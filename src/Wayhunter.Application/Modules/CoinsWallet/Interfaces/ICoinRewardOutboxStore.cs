
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

/// <summary>
/// Claimed reward event with a processing lease.
/// </summary>
public sealed record ClaimedCoinRewardOutboxItem(
    Guid OutboxId,
    string ClaimToken,
    CoinRewardGrantRequest Reward,
    int AttemptNumber);

/// <summary>
/// Future durable M6 reward outbox contract.
///
/// The production implementation MUST:
/// - enqueue with the committed approval transaction
/// - prevent duplicate reward sources
/// - atomically claim due records
/// - recover expired processing leases
/// - persist retry and delivery state
/// </summary>
public interface ICoinRewardOutboxStore
{
    Task<bool> EnqueueIfMissingAsync(
        CoinRewardGrantRequest reward,
        DateTimeOffset queuedAtUtc,
        CancellationToken cancellationToken = default);

    Task<ClaimedCoinRewardOutboxItem?>
        TryClaimNextDueAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default);

    Task MarkDeliveredAsync(
        Guid outboxId,
        string claimToken,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        Guid outboxId,
        string claimToken,
        DateTimeOffset nextDueAtUtc,
        string errorCode,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Source of committed, eligible rewards that might
/// need reconciliation.
///
/// Production implementation must query trusted
/// M1/M4/M5 approval records and support scanning
/// beyond the first batch.
/// </summary>
public interface ICommittedCoinRewardSource
{
    Task<IReadOnlyList<CoinRewardGrantRequest>>
        FindCommittedRewardCandidatesAsync(
            int maxCount,
            CancellationToken cancellationToken = default);
}
