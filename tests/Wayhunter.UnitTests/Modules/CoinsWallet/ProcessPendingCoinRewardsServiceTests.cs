
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class ProcessPendingCoinRewardsServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static CoinRewardGrantRequest Reward(
        CoinRewardType type = CoinRewardType.WelcomeVerified,
        string source = "source-001",
        string owner = "user-001")
    {
        return new CoinRewardGrantRequest(
            EventId: Guid.NewGuid(),
            RewardType: type,
            SourceRecordId: source,
            RecipientUserId: owner,
            CoinsAmount: CoinRewardRules.GetAmount(type),
            EligibilityConfirmedAtUtc: Now.AddMinutes(-10),
            CorrelationId: Guid.NewGuid(),
            RuleVersion: "v1",
            ApprovalRecordId:
                type == CoinRewardType.WelcomeVerified
                    ? null
                    : "admin-review-001",
            GameAttemptId:
                type == CoinRewardType.GameWinnerApproved
                    ? "attempt-001"
                    : null);
    }

    private static ProcessPendingCoinRewardsService Service(
        FakeOutbox outbox,
        FakeLedger ledger,
        params CoinRewardGrantRequest[] rewards)
    {
        return new ProcessPendingCoinRewardsService(
            outbox,
            ledger,
            new FakeSource(rewards));
    }

    [Fact]
    public async Task AllFourRewardSources_ProcessSuccessfully()
    {
        var outbox = new FakeOutbox();
        var ledger = new FakeLedger();

        var service = Service(
            outbox,
            ledger,
            Reward(CoinRewardType.WelcomeVerified, "user-01"),
            Reward(CoinRewardType.HunterApproved, "hunt-01"),
            Reward(CoinRewardType.GameCreatorApproved, "game-01"),
            Reward(CoinRewardType.GameWinnerApproved, "game-02"));

        var result = await service.RunOnceAsync(Now);

        Assert.Equal(4, result.NewlyEnqueued);
        Assert.Equal(4, result.Delivered);
        Assert.Equal(0, result.ScheduledRetries);

        Assert.Equal(4, outbox.DeliveredCount);
        Assert.Equal(500, ledger.CreditedCoins);
    }

    [Fact]
    public async Task RepeatedReconciliation_DoesNotDuplicateCoins()
    {
        var outbox = new FakeOutbox();
        var ledger = new FakeLedger();

        var service = Service(
            outbox,
            ledger,
            Reward());

        var first = await service.RunOnceAsync(Now);
        var second = await service.RunOnceAsync(
            Now.AddMinutes(1));

        Assert.Equal(1, first.NewlyEnqueued);
        Assert.Equal(1, first.Delivered);

        Assert.Equal(0, second.NewlyEnqueued);
        Assert.Equal(0, second.Delivered);

        Assert.Equal(1, outbox.RowCount);
        Assert.Equal(100, ledger.CreditedCoins);
    }

    [Fact]
    public async Task TemporaryFailure_SchedulesRetry()
    {
        var outbox = new FakeOutbox();
        var ledger = new FakeLedger();
        ledger.FailOnce();

        var service = Service(
            outbox,
            ledger,
            Reward());

        var first = await service.RunOnceAsync(Now);

        Assert.Equal(0, first.Delivered);
        Assert.Equal(1, first.ScheduledRetries);
        Assert.Equal(0, ledger.CreditedCoins);

        var tooEarly = await service.RunOnceAsync(
            Now.AddSeconds(10));

        Assert.Equal(0, tooEarly.Delivered);

        var recovered = await service.RunOnceAsync(
            Now.AddSeconds(31));

        Assert.Equal(1, recovered.Delivered);
        Assert.Equal(100, ledger.CreditedCoins);
        Assert.Equal(1, outbox.DeliveredCount);
    }

    [Fact]
    public async Task AcknowledgementFailure_DoesNotDoubleCredit()
    {
        var outbox = new FakeOutbox
        {
            FailNextAck = true
        };

        var ledger = new FakeLedger();

        var service = Service(
            outbox,
            ledger,
            Reward());

        var first = await service.RunOnceAsync(Now);

        Assert.Equal(1, first.ScheduledRetries);
        Assert.Equal(100, ledger.CreditedCoins);

        var retry = await service.RunOnceAsync(
            Now.AddSeconds(31));

        Assert.Equal(1, retry.Delivered);
        Assert.Equal(100, ledger.CreditedCoins);
        Assert.Equal(1, outbox.DeliveredCount);
    }

    [Fact]
    public async Task ConcurrentProcessors_DoNotDuplicate()
    {
        var outbox = new FakeOutbox();
        var ledger = new FakeLedger();

        var service = Service(
            outbox,
            ledger,
            Reward());

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
                service.RunOnceAsync(Now)));

        await Task.WhenAll(tasks);

        Assert.Equal(1, outbox.RowCount);
        Assert.Equal(1, outbox.DeliveredCount);
        Assert.Equal(100, ledger.CreditedCoins);
    }

    [Fact]
    public async Task SameGameWinnerSource_CannotCreditTwoUsers()
    {
        var first = Reward(
            CoinRewardType.GameWinnerApproved,
            "game-001",
            "player-001");

        var second = Reward(
            CoinRewardType.GameWinnerApproved,
            "game-001",
            "player-002");

        var outbox = new FakeOutbox();
        var ledger = new FakeLedger();

        var service = Service(
            outbox,
            ledger,
            first,
            second);

        await service.RunOnceAsync(Now);

        Assert.Equal(1, outbox.RowCount);
        Assert.Equal(100, ledger.CreditedCoins);
    }

    [Fact]
    public async Task InvalidReward_IsNotEnqueued()
    {
        var invalid = Reward() with
        {
            CoinsAmount = 999
        };

        var outbox = new FakeOutbox();
        var ledger = new FakeLedger();

        var service = Service(
            outbox,
            ledger,
            invalid);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RunOnceAsync(Now));

        Assert.Equal(0, outbox.RowCount);
        Assert.Equal(0, ledger.CreditedCoins);
    }

    [Fact]
    public async Task CancelledProcessing_DoesNotEnqueue()
    {
        var outbox = new FakeOutbox();
        var ledger = new FakeLedger();

        var service = Service(
            outbox,
            ledger,
            Reward());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RunOnceAsync(
                Now,
                cancellationToken: cts.Token));

        Assert.Equal(0, outbox.RowCount);
    }

    private sealed class FakeSource : ICommittedCoinRewardSource
    {
        private readonly CoinRewardGrantRequest[] _rewards;

        public FakeSource(CoinRewardGrantRequest[] rewards)
        {
            _rewards = rewards;
        }

        public Task<IReadOnlyList<CoinRewardGrantRequest>>
            FindCommittedRewardCandidatesAsync(
                int maxCount,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<CoinRewardGrantRequest> result =
                _rewards.Take(maxCount).ToArray();

            return Task.FromResult(result);
        }
    }

    private sealed class FakeLedger : ICoinRewardGrantStore
    {
        private readonly ConcurrentDictionary<string, int>
            _credits = new();

        private int _failNext;

        public int CreditedCoins =>
            _credits.Values.Sum();

        public void FailOnce()
        {
            Interlocked.Exchange(ref _failNext, 1);
        }

        public Task<bool> TryGrantOnceAsync(
            CoinRewardGrantRequest reward,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.Exchange(ref _failNext, 0) == 1)
                throw new InvalidOperationException(
                    "Temporary reward failure.");

            string key =
                $"{reward.RewardType}:{reward.SourceRecordId}";

            bool added = _credits.TryAdd(
                key,
                reward.CoinsAmount);

            return Task.FromResult(added);
        }
    }

    private sealed class FakeOutbox : ICoinRewardOutboxStore
    {
        private sealed class Row
        {
            public required CoinRewardGrantRequest Reward;
            public required DateTimeOffset DueAt;
            public string? ClaimToken;
            public DateTimeOffset? LeaseUntil;
            public int Attempts;
            public bool Delivered;
        }

        private readonly object _gate = new();
        private readonly List<Row> _rows = new();

        public bool FailNextAck { get; set; }

        public int RowCount
        {
            get
            {
                lock (_gate)
                    return _rows.Count;
            }
        }

        public int DeliveredCount
        {
            get
            {
                lock (_gate)
                    return _rows.Count(x => x.Delivered);
            }
        }

        public Task<bool> EnqueueIfMissingAsync(
            CoinRewardGrantRequest reward,
            DateTimeOffset queuedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                bool exists = _rows.Any(x =>
                    x.Reward.RewardType == reward.RewardType &&
                    x.Reward.SourceRecordId == reward.SourceRecordId);

                if (exists)
                    return Task.FromResult(false);

                _rows.Add(new Row
                {
                    Reward = reward,
                    DueAt = queuedAtUtc
                });

                return Task.FromResult(true);
            }
        }

        public Task<ClaimedCoinRewardOutboxItem?>
            TryClaimNextDueAsync(
                DateTimeOffset nowUtc,
                TimeSpan leaseDuration,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                var row = _rows.FirstOrDefault(x =>
                    !x.Delivered &&
                    x.DueAt <= nowUtc &&
                    (x.ClaimToken is null ||
                     x.LeaseUntil <= nowUtc));

                if (row is null)
                {
                    return Task.FromResult<
                        ClaimedCoinRewardOutboxItem?>(null);
                }

                row.Attempts++;
                row.ClaimToken = Guid.NewGuid().ToString("N");
                row.LeaseUntil = nowUtc.Add(leaseDuration);

                ClaimedCoinRewardOutboxItem result = new(
                    row.Reward.EventId,
                    row.ClaimToken,
                    row.Reward,
                    row.Attempts);

                return Task.FromResult<
                    ClaimedCoinRewardOutboxItem?>(result);
            }
        }

        public Task MarkDeliveredAsync(
            Guid outboxId,
            string claimToken,
            DateTimeOffset deliveredAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                var row = FindClaim(outboxId, claimToken);

                if (FailNextAck)
                {
                    FailNextAck = false;
                    throw new InvalidOperationException(
                        "Delivery acknowledgement failed.");
                }

                row.Delivered = true;
                row.ClaimToken = null;
                row.LeaseUntil = null;
            }

            return Task.CompletedTask;
        }

        public Task ScheduleRetryAsync(
            Guid outboxId,
            string claimToken,
            DateTimeOffset nextDueAtUtc,
            string errorCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                var row = FindClaim(outboxId, claimToken);

                row.DueAt = nextDueAtUtc;
                row.ClaimToken = null;
                row.LeaseUntil = null;
            }

            return Task.CompletedTask;
        }

        private Row FindClaim(
            Guid outboxId,
            string claimToken)
        {
            var row = _rows.SingleOrDefault(x =>
                x.Reward.EventId == outboxId &&
                x.ClaimToken == claimToken &&
                !x.Delivered);

            return row ?? throw new InvalidOperationException(
                "Outbox claim no longer valid.");
        }
    }
}
