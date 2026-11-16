
using System;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;

namespace Wayhunter.Api.Extensions;

public static class HomeAdsAnalyticsServiceCollectionExtensions
{
    // Registers M7 application services.
    // SQL DbContext registration is intentionally deferred
    // until shared migration ownership is approved.
    public static IServiceCollection AddHomeAdsAnalyticsFoundation(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<
            IAdvertisementRepository,
            AdvertisementRepository>();

        services.AddScoped<AdvertisementService>();

        return services;
    }
}
