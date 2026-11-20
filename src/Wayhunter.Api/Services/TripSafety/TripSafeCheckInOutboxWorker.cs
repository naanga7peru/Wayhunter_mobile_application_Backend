using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

namespace Wayhunter.Api.Services.TripSafety;

// Intentionally unregistered until the database and sender are approved.
public sealed class TripSafeCheckInOutboxWorker(
    IServiceScopeFactory scopes, M3OutboxWorkerSettings settings, ILogger<TripSafeCheckInOutboxWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        settings.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var index = 0; index < settings.BatchSize; index++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<TripSafeCheckInOutboxDispatcher>();
                    if (await dispatcher.ProcessNextAsync(stoppingToken) == TripSafeCheckInDispatchOutcome.NoWork)
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // Avoid logging recipient IDs, payloads, or provider response details.
                logger.LogWarning("M3 safe check-in outbox processing failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.PollSeconds), stoppingToken);
        }
    }
}
