
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

public sealed record CoinRewardOutboxRunResult(
    int NewlyEnqueued,
    int Delivered,
    int ScheduledRetries);

/// <summary>
/// M6-MS03-T05 reward processing foundation.
///
/// A hosted worker can call RunOnceAsync after
/// a durable SQL outbox and reconciliation source
/// have been implemented.
///
/// This class itself does not schedule background jobs.
/// </summary>
public sealed class ProcessPendingCoinRewardsService
{
    private static readonly TimeSpan ProcessingLease =
        TimeSpan.FromMinutes(5);

    private readonly ICoinRewardOutboxStore _outbox;
    private readonly ICoinRewardGrantStore _rewardStore;
    private readonly ICommittedCoinRewardSource _source;

    public ProcessPendingCoinRewardsService(
        ICoinRewardOutboxStore outbox,
        ICoinRewardGrantStore rewardStore,
        ICommittedCoinRewardSource source)
    {
        _outbox = outbox ??
            throw new ArgumentNullException(nameof(outbox));

        _rewardStore = rewardStore ??
            throw new ArgumentNullException(nameof(rewardStore));

        _source = source ??
            throw new ArgumentNullException(nameof(source));
    }

    public async Task<CoinRewardOutboxRunResult> RunOnceAsync(
        DateTimeOffset nowUtc,
        int maxBatch = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (nowUtc == default ||
            nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A valid UTC timestamp is required.",
                nameof(nowUtc));
        }

        if (maxBatch < 1 || maxBatch > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBatch));
        }

        int enqueued = 0;
        int delivered = 0;
        int retries = 0;

        // 1. Reconcile trusted, committed rewards.
        var candidates =
            await _source.FindCommittedRewardCandidatesAsync(
                maxBatch,
                cancellationToken);

        foreach (var reward in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CoinRewardContractRules.ValidateGrant(reward);

            bool added = await _outbox.EnqueueIfMissingAsync(
                reward,
                nowUtc,
                cancellationToken);

            if (added)
                enqueued++;
        }

        // 2. Claim and process due outbox records.
        for (int i = 0; i < maxBatch; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var claimed = await _outbox.TryClaimNextDueAsync(
                nowUtc,
                ProcessingLease,
                cancellationToken);

            if (claimed is null)
                break;

            try
            {
                // true  = new reward credited
                // false = already credited (idempotent replay)
                //
                // Both outcomes mean delivery is complete.
                await _rewardStore.TryGrantOnceAsync(
                    claimed.Reward,
                    cancellationToken);

                await _outbox.MarkDeliveredAsync(
                    claimed.OutboxId,
                    claimed.ClaimToken,
                    nowUtc,
                    cancellationToken);

                delivered++;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                // Retry an unacknowledged reward.
                // A prior successful credit must be
                // deduplicated by the durable ledger.
                var nextDue = nowUtc.Add(
                    CalculateRetryDelay(
                        claimed.AttemptNumber));

                await _outbox.ScheduleRetryAsync(
                    claimed.OutboxId,
                    claimed.ClaimToken,
                    nextDue,
                    error.GetType().Name,
                    cancellationToken);

                retries++;
            }
        }

        return new CoinRewardOutboxRunResult(
            enqueued,
            delivered,
            retries);
    }

    private static TimeSpan CalculateRetryDelay(
        int attemptNumber)
    {
        // Exponential backoff capped at one hour.
        int exponent = Math.Clamp(
            attemptNumber - 1,
            0,
            7);

        int seconds = Math.Min(
            3600,
            30 * (1 << exponent));

        return TimeSpan.FromSeconds(seconds);
    }
}
