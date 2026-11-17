
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Api.Extensions;
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

        return services;
    }
}
