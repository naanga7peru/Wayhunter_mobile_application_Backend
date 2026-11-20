using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

namespace Wayhunter.Api.Services.TripSafety;

// Intentionally unregistered. Existing outbox workers keep their own event contracts.
public sealed class NotificationDispatchWorker(IServiceScopeFactory scopes, M3OutboxWorkerSettings settings,
    ILogger<NotificationDispatchWorker> logger) : BackgroundService
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
                    var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatchService>();
                    if (await dispatcher.ProcessNextAsync(stoppingToken) == NotificationDispatchOutcome.NoWork)
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                logger.LogWarning("MS08 notification processing failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(settings.PollSeconds), stoppingToken);
        }
    }
}
