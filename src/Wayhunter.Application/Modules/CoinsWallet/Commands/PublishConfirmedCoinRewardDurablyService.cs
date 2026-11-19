using Wayhunter.Application.Modules.CoinsWallet.Interfaces;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

/// <summary>
/// Persists independent consumer outcomes. Consumers still deduplicate by
/// DeliveryKey so an uncertain outcome can be retried safely.
/// </summary>
public sealed class PublishConfirmedCoinRewardDurablyService
{
    private readonly ICoinRewardConsumerDeliveryStore _deliveries;
    private readonly ICoinRewardNotificationConsumer _notifications;
    private readonly ICoinRewardRankingConsumer _ranking;
    private readonly ICoinRewardProfileConsumer _profile;

    public PublishConfirmedCoinRewardDurablyService(
        ICoinRewardConsumerDeliveryStore deliveries,
        ICoinRewardNotificationConsumer notifications,
        ICoinRewardRankingConsumer ranking,
        ICoinRewardProfileConsumer profile)
    {
        _deliveries = deliveries;
        _notifications = notifications;
        _ranking = ranking;
        _profile = profile;
    }

    public async Task<CoinRewardConsumerPublishResult> PublishAsync(
        CoinRewardConfirmedEvent rewardEvent,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rewardEvent);
        CoinRewardConfirmedEventFactory.ValidateConfirmed(rewardEvent);
        if (rewardEvent.RewardEventId == Guid.Empty)
            throw new InvalidOperationException(
                "Durable publication requires the reward outbox event ID.");
        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Valid UTC timestamp required.", nameof(nowUtc));

        bool notified = await DeliverAsync(
            "notification", rewardEvent,
            () => _notifications.PublishOnceAsync(rewardEvent, cancellationToken),
            nowUtc, cancellationToken);
        bool ranked = await DeliverAsync(
            "ranking", rewardEvent,
            () => _ranking.PublishOnceAsync(rewardEvent, cancellationToken),
            nowUtc, cancellationToken);
        bool profiled = await DeliverAsync(
            "profile", rewardEvent,
            () => _profile.PublishOnceAsync(rewardEvent, cancellationToken),
            nowUtc, cancellationToken);

        return new CoinRewardConsumerPublishResult(notified, ranked)
        {
            ProfilePublished = profiled
        };
    }

    private async Task<bool> DeliverAsync(
        string consumerName,
        CoinRewardConfirmedEvent rewardEvent,
        Func<Task<bool>> publish,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (await _deliveries.IsDeliveredAsync(
                rewardEvent.RewardEventId, consumerName, cancellationToken))
            return false;

        try
        {
            bool published = await publish();
            await _deliveries.RecordOutcomeAsync(
                rewardEvent.RewardEventId, consumerName, true,
                nowUtc, null, cancellationToken);
            return published;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            await _deliveries.RecordOutcomeAsync(
                rewardEvent.RewardEventId, consumerName, false,
                nowUtc, error.GetType().Name, cancellationToken);
            return false;
        }
    }
}
