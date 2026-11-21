using Wayhunter.Api.Services.HomeAdsAnalytics;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;

namespace Wayhunter.Api.Extensions;

public static class HomeAdsAnalyticsServiceCollectionExtensions
{
    // Registers M7 services only. Callers that need SQL must use
    // AddHomeAdsAnalyticsSqlPersistence so the authoritative shared
    // DbContext is registered exactly once.
    public static IServiceCollection AddHomeAdsAnalyticsFoundation(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Advertisement read repository
        services.TryAddScoped<
            IAdvertisementRepository,
            AdvertisementRepository>();

        // Advertisement write repository
        services.TryAddScoped<
            IAdvertisementWriteRepository,
            AdvertisementRepository>();

        // Advertisement image-only persistence
        services.TryAddScoped<
            IAdvertisementImageUpdateRepository,
            AdvertisementRepository>();
        // Advertisement application service
        services.TryAddScoped<AdvertisementService>();
        services.TryAddScoped<AdvertisementImageLifecycleService>();

        return services;
    }

    public static IServiceCollection AddHomeAdsAnalyticsSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddWayhunterSharedPersistence(configuration);
        services.AddHomeAdsAnalyticsFoundation();

        return services;
    }
}
