
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Infrastructure.Integrations.Ai;

namespace Wayhunter.Api.Extensions;

public static class TravelPlanningAiConfigurationExtensions
{
    // Explicit configuration for unit tests.
    public static IServiceCollection AddTravelPlanningAiConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AiTripProviderOptions>()
            .Bind(configuration.GetSection(
                AiTripProviderOptions.SectionName))
            .Validate(
                options => options.Validate().Count == 0,
                "AI provider configuration is invalid.")
            .ValidateOnStart();

        return services;
    }

    // Runtime configuration from Dependency Injection.
    public static IServiceCollection AddTravelPlanningAiConfiguration(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<AiTripProviderOptions>()
            .Configure<IConfiguration>(
                (options, configuration) =>
                {
                    configuration
                        .GetSection(
                            AiTripProviderOptions.SectionName)
                        .Bind(options);
                })
            .Validate(
                options => options.Validate().Count == 0,
                "AI provider configuration is invalid.")
            .ValidateOnStart();

        return services;
    }
}
