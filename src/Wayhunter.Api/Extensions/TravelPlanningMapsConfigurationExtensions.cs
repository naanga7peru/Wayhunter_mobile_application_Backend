
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Infrastructure.Integrations.Maps;

namespace Wayhunter.Api.Extensions;

public static class TravelPlanningMapsConfigurationExtensions
{
    // Used by unit tests with an explicit configuration.
    public static IServiceCollection AddTravelPlanningMapsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<MapsProviderOptions>()
            .Bind(configuration.GetSection(
                MapsProviderOptions.SectionName))
            .Validate(
                options => options.Validate().Count == 0,
                "An enabled Maps provider is missing required server configuration.")
            .ValidateOnStart();

        return services;
    }

    // Used by the application module.
    // IConfiguration is resolved from dependency injection.
    public static IServiceCollection AddTravelPlanningMapsConfiguration(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<MapsProviderOptions>()
            .Configure<IConfiguration>(
                (options, configuration) =>
                {
                    configuration
                        .GetSection(MapsProviderOptions.SectionName)
                        .Bind(options);
                })
            .Validate(
                options => options.Validate().Count == 0,
                "An enabled Maps provider is missing required server configuration.")
            .ValidateOnStart();

        return services;
    }
}
