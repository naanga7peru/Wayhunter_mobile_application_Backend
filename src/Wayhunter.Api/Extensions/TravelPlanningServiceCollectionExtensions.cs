
using Wayhunter.Infrastructure.Integrations.Ai;
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
        services.AddTravelPlanningAiConfiguration();

        // M2-MS07: Gemini AI Trip Generation
        // Keep Gemini API credentials on the backend.
        //
        // Default configuration is disabled.
        // The real HTTP provider is only called when enabled.

        services.AddHttpClient<
            IAiTripGenerationProvider,
            GeminiTripGenerationProvider>();

        services.AddScoped<GenerateAiTripPlanService>();

        services.AddSingleton<InMemoryAiTripDailyQuota>(
            _ => new InMemoryAiTripDailyQuota(TimeProvider.System));


        // Weather provider and shared cache.
        services.AddTravelPlanningWeather();

        // Temporary in-memory Trip repository.
        // SQL integration requires separate approval.
        services.AddSingleton<
            ITripPlanRepository,
            InMemoryTripPlanRepository>();

        // M2 - Trip Planning Services
        services.AddScoped<CreateTripPlanService>();
        services.AddScoped<GetTripPlanService>();
        services.AddScoped<UpdateTripPlanService>();
        services.AddScoped<CancelTripPlanService>();

        // M2 - Add Trip Stop API (NEW)
        services.AddScoped<AddTripStopService>();

        services.AddScoped<RemoveTripStopService>();

        services.AddScoped<ReorderTripStopsService>();

        // M2-MS05-T01: privacy-gated suggested plans.
        // Demo mode has no M3 completion evidence and returns no items.
        services.AddSingleton<
            ISuggestedTripPlansReader,
            UnavailableSuggestedTripPlansReader>();
        services.AddScoped<GetSuggestedTripPlansService>();

        // M2-MS05-T03: default is fail-closed until reuse consent policy
        // and shared SQL schema are explicitly approved.
        services.AddSingleton<
            ICopySuggestedTripPlanStore,
            UnavailableCopySuggestedTripPlanStore>();

        services.AddScoped<CopySuggestedTripPlanService>();

        // M2-MS06: Historical Route Segment Reuse
        //
        // The real M3 verified-stop reader is not yet available.
        // Fail closed until M2/M3 approve trusted evidence integration.
        services.AddSingleton<
            ITrustedHistoricalEvidenceReader,
            UnavailableTrustedHistoricalEvidenceReader>();

        services.AddScoped<GetHistoricalRouteSegmentsService>();

        return services;

    }
}
