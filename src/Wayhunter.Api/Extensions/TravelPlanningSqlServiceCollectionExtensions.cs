
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

namespace Wayhunter.Api.Extensions;

public static class TravelPlanningSqlServiceCollectionExtensions
{
    public static IServiceCollection AddTravelPlanningSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Reuse the single shared DbContext registration.
        services.AddWayhunterSharedPersistence(configuration);

        // Replace temporary InMemory repository with SQL repository.
        services.Replace(
            ServiceDescriptor.Scoped<
                ITripPlanRepository,
                EfTripPlanRepository>());

        // M2 and M3 must share approved SQL persistence before the
        // completion + membership query is usable. Otherwise fail closed.
        if (configuration.GetValue<bool>(
            "TripSafety:EnableSqlPersistence"))
        {
            services.Replace(
                ServiceDescriptor.Scoped<
                    ISuggestedTripPlansReader,
                    SqlSuggestedTripPlansReader>());
        }

        return services;
    }

    public static IServiceCollection AddConfiguredTravelPlanningPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var enableSql = configuration.GetValue<bool>(
            "TravelPlanning:EnableSqlPersistence");

        if (enableSql)
        {
            services.AddTravelPlanningSqlPersistence(configuration);
        }

        return services;
    }
}
