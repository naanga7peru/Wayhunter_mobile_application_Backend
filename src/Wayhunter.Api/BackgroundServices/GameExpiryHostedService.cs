using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wayhunter.Application.Modules.Games.Services;

namespace Wayhunter.Api.BackgroundServices;

public sealed class GameExpiryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameExpiryHostedService> _logger;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;

    public GameExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<GameExpiryHostedService> logger,
        TimeSpan interval,
        int batchSize)
    {
        _scopeFactory = scopeFactory
            ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval > TimeSpan.Zero
            ? interval
            : throw new ArgumentOutOfRangeException(nameof(interval));
        _batchSize = batchSize > 0
            ? batchSize
            : throw new ArgumentOutOfRangeException(nameof(batchSize));
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ArchiveDueGamesAsync(stoppingToken);

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }

    private async Task ArchiveDueGamesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ArchiveExpiredGamesService>();
            var archivedCount = await service.ArchiveBatchAsync(
                _batchSize, cancellationToken);

            if (archivedCount > 0)
            {
                _logger.LogInformation(
                    "Archived {ArchivedGameCount} expired games.",
                    archivedCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "The expired game archive pass failed.");
        }
    }
}
