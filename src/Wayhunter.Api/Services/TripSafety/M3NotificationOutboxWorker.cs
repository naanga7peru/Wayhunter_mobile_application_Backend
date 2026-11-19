
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

namespace Wayhunter.Api.Services.TripSafety;

public sealed class M3NotificationOutboxWorker
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly M3OutboxWorkerSettings _settings;
    private readonly ILogger<M3NotificationOutboxWorker> _logger;

    public M3NotificationOutboxWorker(
        IServiceScopeFactory scopeFactory,
        M3OutboxWorkerSettings settings,
        ILogger<M3NotificationOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory ??
            throw new ArgumentNullException(nameof(scopeFactory));

        _settings = settings ??
            throw new ArgumentNullException(nameof(settings));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));

        _settings.Validate();
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "M3 membership revocation notification worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var dispatcher = scope.ServiceProvider
                    .GetRequiredService<M3NotificationOutboxDispatcher>();

                for (var index = 0;
                     index < _settings.BatchSize;
                     index++)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    var outcome =
                        await dispatcher.ProcessNextAsync(
                            stoppingToken);

                    if (outcome == M3OutboxDispatchOutcome.NoWork)
                    {
                        break;
                    }

                    switch (outcome)
                    {
                        case M3OutboxDispatchOutcome.Delivered:
                            _logger.LogInformation(
                                "M3 notification delivery acknowledged.");
                            break;

                        case M3OutboxDispatchOutcome.RetryScheduled:
                            _logger.LogWarning(
                                "M3 notification delivery failed; retry scheduled.");
                            break;

                        case M3OutboxDispatchOutcome.DeadLettered:
                            _logger.LogError(
                                "M3 notification exhausted delivery attempts.");
                            break;
                    }
                }

                // Aggregate counters only.
                // Do not log member IDs or payload data.
                var status = await dispatcher.GetStatusAsync(
                    stoppingToken);

                _logger.LogInformation(
                    "M3 outbox metrics: Ready={Ready}, Delayed={Delayed}, " +
                    "Leased={Leased}, DeadLettered={DeadLettered}, " +
                    "Delivered={Delivered}, Exhausted={Exhausted}",
                    status.Ready,
                    status.Delayed,
                    status.Leased,
                    status.DeadLettered,
                    status.Delivered,
                    status.Exhausted);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "M3 outbox processing cycle failed.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_settings.PollSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "M3 membership revocation notification worker stopped.");
    }
}
