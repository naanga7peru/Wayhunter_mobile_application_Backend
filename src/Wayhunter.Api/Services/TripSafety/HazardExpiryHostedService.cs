
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Wayhunter.Application.Modules.TripSafety.Services;

namespace Wayhunter.Api.Services.TripSafety;

public sealed class HazardExpiryHostedService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HazardExpiryHostedService> _logger;

    public HazardExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<HazardExpiryHostedService> logger)
    {
        _scopeFactory = scopeFactory ??
            throw new ArgumentNullException(nameof(scopeFactory));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var service = scope.ServiceProvider
                    .GetRequiredService<TripHazardService>();

                var expired = await service.ExpireDueAsync(
                    stoppingToken);

                if (expired > 0)
                {
                    _logger.LogInformation(
                        "M3 hazard expiry marked {Count} records expired.",
                        expired);
                }
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
                    "M3 hazard expiry pass failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(
                    stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
