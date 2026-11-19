using Wayhunter.Application.Modules.CoinsWallet.Commands;

namespace Wayhunter.Api.BackgroundServices;

public sealed class CoinRewardOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CoinRewardOutboxWorker> _logger;
    private readonly TimeSpan _interval;

    public CoinRewardOutboxWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IConfiguration configuration,
        ILogger<CoinRewardOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
        int seconds = Math.Clamp(configuration.GetValue(
            "CoinsWallet:RewardOutboxIntervalSeconds", 30), 5, 3600);
        _interval = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval, _timeProvider);
        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<ProcessPendingCoinRewardsService>();
                await processor.RunOnceAsync(
                    _timeProvider.GetUtcNow(), 100, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                _logger.LogError(error,
                    "M6 reward outbox pass failed; pending records remain retryable.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
