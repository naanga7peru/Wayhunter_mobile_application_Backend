
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

        // Register server-side Maps configuration.
        services.AddTravelPlanningMapsConfiguration();

        // Temporary in-memory Trip repository.
        // SQL integration awaits shared migration approval.
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
