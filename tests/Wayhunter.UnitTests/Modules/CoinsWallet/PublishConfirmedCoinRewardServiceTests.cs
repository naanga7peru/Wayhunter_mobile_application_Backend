
using System;
using System.Collections.Concurrent;
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

public sealed class PublishConfirmedCoinRewardServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static CoinRewardConfirmedEvent Event(
        CoinRewardType type = CoinRewardType.WelcomeVerified,
        string source = "source-001")
    {
        var request = new CoinRewardGrantRequest(
            EventId: Guid.NewGuid(),
            RewardType: type,
            SourceRecordId: source,
            RecipientUserId: "user-001",
            CoinsAmount: CoinRewardRules.GetAmount(type),
            EligibilityConfirmedAtUtc: Now.AddMinutes(-1),
            CorrelationId: Guid.NewGuid(),
            RuleVersion: "v1",
            ApprovalRecordId:
                type == CoinRewardType.WelcomeVerified
                    ? null : "approval-001",
            GameAttemptId:
                type == CoinRewardType.GameWinnerApproved
                    ? "attempt-001" : null);

        return CoinRewardConfirmedEventFactory
            .CreateFromCommittedLedger(
                request, Guid.NewGuid(), Now);
    }

    [Theory]
    [InlineData(
        CoinRewardType.WelcomeVerified,
        CoinRewardSourceModule.IdentitySocial,
        "verified-account")]
    [InlineData(
        CoinRewardType.HunterApproved,
        CoinRewardSourceModule.HunterPlaces,
        "approved-place")]
    [InlineData(
        CoinRewardType.GameCreatorApproved,
        CoinRewardSourceModule.Games,
        "approved-game-creator")]
    [InlineData(
        CoinRewardType.GameWinnerApproved,
        CoinRewardSourceModule.Games,
        "approved-game-winner")]
    public void AllSources_HaveConfirmedPayload(
        CoinRewardType type,
        CoinRewardSourceModule module,
        string category)
    {
        var rewardEvent = Event(type);

        Assert.Equal(module, rewardEvent.SourceModule);
        Assert.Equal(category, rewardEvent.SourceCategory);
        Assert.Equal(
            CoinRewardConfirmationState.Confirmed,
            rewardEvent.State);

        Assert.NotEqual(
            Guid.Empty, rewardEvent.LedgerEntryId);
        Assert.False(string.IsNullOrWhiteSpace(
            rewardEvent.DeliveryKey));
    }

    [Fact]
    public async Task ConfirmedEvent_PublishesToBoth()
    {
        var notification = new FakeConsumer();
        var ranking = new FakeConsumer();

        var service = new PublishConfirmedCoinRewardService(
            notification, ranking);

        var result = await service.PublishAsync(Event());

        Assert.True(result.NotificationPublished);
        Assert.True(result.RankingPublished);
        Assert.Equal(1, notification.Count);
        Assert.Equal(1, ranking.Count);
    }

    [Fact]
    public async Task DuplicateDelivery_IsIgnored()
    {
        var notification = new FakeConsumer();
        var ranking = new FakeConsumer();

        var service = new PublishConfirmedCoinRewardService(
            notification, ranking);

        var rewardEvent = Event();

        await service.PublishAsync(rewardEvent);

        var retry = await service.PublishAsync(rewardEvent);

        Assert.False(retry.NotificationPublished);
        Assert.False(retry.RankingPublished);
        Assert.Equal(1, notification.Count);
        Assert.Equal(1, ranking.Count);
    }

    [Fact]
    public async Task RankingFailure_CanRetrySafely()
    {
        var notification = new FakeConsumer();
        var ranking = new FakeConsumer();

        ranking.FailOnce();

        var service = new PublishConfirmedCoinRewardService(
            notification, ranking);

        var rewardEvent = Event();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PublishAsync(rewardEvent));

        Assert.Equal(1, notification.Count);
        Assert.Equal(0, ranking.Count);

        var retry = await service.PublishAsync(rewardEvent);

        Assert.False(retry.NotificationPublished);
        Assert.True(retry.RankingPublished);

        Assert.Equal(1, notification.Count);
        Assert.Equal(1, ranking.Count);
    }

    [Fact]
    public async Task ConcurrentDeliveries_StayIdempotent()
    {
        var notification = new FakeConsumer();
        var ranking = new FakeConsumer();

        var service = new PublishConfirmedCoinRewardService(
            notification, ranking);

        var rewardEvent = Event();

        await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => Task.Run(() =>
                    service.PublishAsync(rewardEvent))));

        Assert.Equal(1, notification.Count);
        Assert.Equal(1, ranking.Count);
    }

    [Fact]
    public async Task InvalidConfirmedState_IsRejected()
    {
        var service = new PublishConfirmedCoinRewardService(
            new FakeConsumer(), new FakeConsumer());

        var invalid = Event() with
        {
            State = (CoinRewardConfirmationState)0
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PublishAsync(invalid));
    }

    [Fact]
    public async Task InvalidSourceCategory_IsRejected()
    {
        var service = new PublishConfirmedCoinRewardService(
            new FakeConsumer(), new FakeConsumer());

        var invalid = Event() with
        {
            SourceCategory = "untrusted-source"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PublishAsync(invalid));
    }

    [Fact]
    public async Task Cancellation_DoesNotDeliver()
    {
        var notification = new FakeConsumer();
        var ranking = new FakeConsumer();

        var service = new PublishConfirmedCoinRewardService(
            notification, ranking);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.PublishAsync(
                Event(), cts.Token));

        Assert.Equal(0, notification.Count);
        Assert.Equal(0, ranking.Count);
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
                throw new InvalidOperationException(
                    "Simulated consumer failure.");

            return Task.FromResult(
                _delivered.TryAdd(
                    rewardEvent.DeliveryKey, 0));
        }
    }
}
