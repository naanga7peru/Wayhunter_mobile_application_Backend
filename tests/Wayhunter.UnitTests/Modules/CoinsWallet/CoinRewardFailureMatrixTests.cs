
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class CoinRewardFailureMatrixTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static CoinRewardGrantRequest Reward(
        CoinRewardType type,
        string source,
        string owner = "user-001")
    {
        return new CoinRewardGrantRequest(
            EventId: Guid.NewGuid(),
            RewardType: type,
            SourceRecordId: source,
            RecipientUserId: owner,
            CoinsAmount: CoinRewardRules.GetAmount(type),
            EligibilityConfirmedAtUtc: Now.AddMinutes(-5),
            CorrelationId: Guid.NewGuid(),
            RuleVersion: "v1",
            ApprovalRecordId:
                type == CoinRewardType.WelcomeVerified
                    ? null : "approval-001",
            GameAttemptId:
                type == CoinRewardType.GameWinnerApproved
                    ? "attempt-001" : null);
    }

    private static CoinRewardGrantRequest[] AllSources()
    {
        return new[]
        {
            Reward(
                CoinRewardType.WelcomeVerified,
                "verified-user-001"),

            Reward(
                CoinRewardType.HunterApproved,
                "hunter-submission-001"),

            Reward(
                CoinRewardType.GameCreatorApproved,
                "game-creator-001"),

            Reward(
                CoinRewardType.GameWinnerApproved,
                "game-winner-001")
        };
    }

    [Fact]
    public async Task FourSources_RetryWithoutDuplicateCredit()
    {
        var ledger = new FakeAtomicLedger();
        var notification = new FakeConsumer();
        var ranking = new FakeConsumer();

        var service = new PublishConfirmedCoinRewardService(
            notification, ranking);

        var rewards = AllSources();

        foreach (var reward in rewards)
        {
            var first = await ledger.CreditOnceAsync(reward);

            Assert.True(first.Added);

            var confirmed =
                CoinRewardConfirmedEventFactory
                    .CreateFromCommittedLedger(
                        reward,
                        first.LedgerEntryId,
                        Now);

            await service.PublishAsync(confirmed);
        }

        // Reconcile the same four rewards again.
        foreach (var reward in rewards)
        {
            var replay = await ledger.CreditOnceAsync(reward);

            Assert.False(replay.Added);

            var confirmed =
                CoinRewardConfirmedEventFactory
                    .CreateFromCommittedLedger(
                        reward,
                        replay.LedgerEntryId,
                        Now);

            await service.PublishAsync(confirmed);
        }

        Assert.Equal(4, ledger.EntryCount);
        Assert.Equal(500, ledger.TotalCoins);
        Assert.Equal(4, notification.Count);
        Assert.Equal(4, ranking.Count);
    }

    [Fact]
    public async Task FailedLedgerWrite_RetryCreditsOnlyOnce()
    {
        var ledger = new FakeAtomicLedger();
        ledger.FailNextWrite();

        var reward = Reward(
            CoinRewardType.HunterApproved,
            "hunter-002");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ledger.CreditOnceAsync(reward));

        Assert.Equal(0, ledger.EntryCount);
        Assert.Equal(0, ledger.TotalCoins);

        var retry = await ledger.CreditOnceAsync(reward);

        Assert.True(retry.Added);
        Assert.Equal(1, ledger.EntryCount);
        Assert.Equal(200, ledger.TotalCoins);
    }

    [Fact]
    public async Task RankingFailure_AfterCredit_IsRetrySafe()
    {
        var ledger = new FakeAtomicLedger();
        var notification = new FakeConsumer();
        var ranking = new FakeConsumer();

        ranking.FailOnce();

        var publisher = new PublishConfirmedCoinRewardService(
            notification, ranking);

        var reward = Reward(
            CoinRewardType.WelcomeVerified,
            "user-003");

        var committed = await ledger.CreditOnceAsync(reward);

        var confirmed =
            CoinRewardConfirmedEventFactory
                .CreateFromCommittedLedger(
                    reward,
                    committed.LedgerEntryId,
                    Now);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(confirmed));

        // A replay finds the original ledger entry.
        var retry = await ledger.CreditOnceAsync(reward);

        Assert.False(retry.Added);

        await publisher.PublishAsync(confirmed);

        Assert.Equal(1, ledger.EntryCount);
        Assert.Equal(100, ledger.TotalCoins);
        Assert.Equal(1, notification.Count);
        Assert.Equal(1, ranking.Count);
    }

    [Fact]
    public async Task NotificationFailure_CanRetry()
    {
        var ledger = new FakeAtomicLedger();
        var notification = new FakeConsumer();
        var ranking = new FakeConsumer();

        notification.FailOnce();

        var publisher = new PublishConfirmedCoinRewardService(
            notification, ranking);

        var reward = Reward(
            CoinRewardType.GameCreatorApproved,
            "game-004");

        var result = await ledger.CreditOnceAsync(reward);

        var confirmed =
            CoinRewardConfirmedEventFactory
                .CreateFromCommittedLedger(
                    reward,
                    result.LedgerEntryId,
                    Now);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(confirmed));

        Assert.Equal(0, notification.Count);
        Assert.Equal(0, ranking.Count);

        await publisher.PublishAsync(confirmed);

        Assert.Equal(1, ledger.EntryCount);
        Assert.Equal(100, ledger.TotalCoins);
        Assert.Equal(1, notification.Count);
        Assert.Equal(1, ranking.Count);
    }

    [Fact]
    public async Task ConcurrentHunterCredits_AreUnique()
    {
        var ledger = new FakeAtomicLedger();

        var reward = Reward(
            CoinRewardType.HunterApproved,
            "hunter-005");

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () => ledger.CreditOnceAsync(reward)));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(x => x.Added));
        Assert.Equal(1, ledger.EntryCount);
        Assert.Equal(200, ledger.TotalCoins);
    }

    [Fact]
    public async Task SameGameWinner_TwoUsersCannotBothCredit()
    {
        var ledger = new FakeAtomicLedger();

        var first = Reward(
            CoinRewardType.GameWinnerApproved,
            "same-game-006",
            "player-001");

        var second = Reward(
            CoinRewardType.GameWinnerApproved,
            "same-game-006",
            "player-002");

        Assert.True(
            (await ledger.CreditOnceAsync(first)).Added);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ledger.CreditOnceAsync(second));

        Assert.Equal(1, ledger.EntryCount);
        Assert.Equal(100, ledger.TotalCoins);
    }

    [Fact]
    public async Task InvalidAmount_DoesNotModifyLedger()
    {
        var ledger = new FakeAtomicLedger();

        var invalid = Reward(
            CoinRewardType.WelcomeVerified,
            "invalid-007") with
        {
            CoinsAmount = 999
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => ledger.CreditOnceAsync(invalid));

        Assert.Equal(0, ledger.EntryCount);
        Assert.Equal(0, ledger.TotalCoins);
    }

    [Fact]
    public void MissingPersistedLedgerId_CannotPublish()
    {
        var reward = Reward(
            CoinRewardType.WelcomeVerified,
            "missing-ledger-008");

        Assert.Throws<ArgumentException>(() =>
            CoinRewardConfirmedEventFactory
                .CreateFromCommittedLedger(
                    reward,
                    Guid.Empty,
                    Now));
    }

    // Test-only transactional simulation.
    // This is not a SQL database implementation.
    private sealed class FakeAtomicLedger
    {
        private sealed record Entry(
            Guid LedgerId,
            string Owner,
            int Coins);

        private readonly object _gate = new();
        private readonly Dictionary<string, Entry>
            _entries = new(StringComparer.Ordinal);

        private bool _failNext;

        public int EntryCount
        {
            get
            {
                lock (_gate)
                    return _entries.Count;
            }
        }

        public int TotalCoins
        {
            get
            {
                lock (_gate)
                    return _entries.Values.Sum(x => x.Coins);
            }
        }

        public void FailNextWrite()
        {
            lock (_gate)
                _failNext = true;
        }

        public Task<(bool Added, Guid LedgerEntryId)>
            CreditOnceAsync(
                CoinRewardGrantRequest reward)
        {
            CoinRewardContractRules.ValidateGrant(reward);

            string key =
                CoinRewardContractRules.BuildIdempotencyKey(
                    reward.RewardType,
                    reward.SourceRecordId);

            lock (_gate)
            {
                if (_failNext)
                {
                    _failNext = false;
                    throw new InvalidOperationException(
                        "Simulated failed transaction.");
                }

                if (_entries.TryGetValue(
                    key, out var existing))
                {
                    if (existing.Owner !=
                        reward.RecipientUserId)
                    {
                        throw new InvalidOperationException(
                            "Conflicting reward recipient.");
                    }

                    return Task.FromResult(
                        (false, existing.LedgerId));
                }

                Guid ledgerId = Guid.NewGuid();

                _entries.Add(
                    key,
                    new Entry(
                        ledgerId,
                        reward.RecipientUserId,
                        reward.CoinsAmount));

                return Task.FromResult(
                    (true, ledgerId));
            }
        }
    }

    private sealed class FakeConsumer :
        ICoinRewardNotificationConsumer,
        ICoinRewardRankingConsumer
    {
        private readonly ConcurrentDictionary<string, byte>
            _delivered = new(StringComparer.Ordinal);

        private int _failNext;

        public int Count => _delivered.Count;

        public void FailOnce()
        {
            Interlocked.Exchange(ref _failNext, 1);
        }

        public Task<bool> PublishOnceAsync(
            CoinRewardConfirmedEvent rewardEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.Exchange(ref _failNext, 0) == 1)
            {
                throw new InvalidOperationException(
                    "Simulated consumer failure.");
            }

            bool added = _delivered.TryAdd(
                rewardEvent.DeliveryKey, 0);

            return Task.FromResult(added);
        }
    }
}
