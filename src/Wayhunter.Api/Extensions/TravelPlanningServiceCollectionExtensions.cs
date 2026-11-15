
using System;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

namespace Wayhunter.Api.Extensions;

public static class TravelPlanningServiceCollectionExtensions
{
    public static IServiceCollection AddTravelPlanningModule(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Server-side Maps configuration.
        services.AddTravelPlanningMapsConfiguration();

        // Server-side AI provider configuration.
        // Actual AI provider implementation is added later.
        services.AddTravelPlanningAiConfiguration();

        // Temporary in-memory Trip repository.
        // SQL integration awaits migration approval.
        services.AddSingleton<
            ITripPlanRepository,
            InMemoryTripPlanRepository>();

        services.AddScoped<CreateTripPlanService>();
        services.AddScoped<GetTripPlanService>();
        services.AddScoped<UpdateTripPlanService>();
        services.AddScoped<CancelTripPlanService>();

        return services;
    }
}
