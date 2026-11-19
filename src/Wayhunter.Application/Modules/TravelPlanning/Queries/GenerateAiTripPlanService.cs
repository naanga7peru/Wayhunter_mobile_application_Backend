
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Validators;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

public sealed class GenerateAiTripPlanService
{
    private readonly IAiTripGenerationProvider _provider;

    public GenerateAiTripPlanService(
        IAiTripGenerationProvider provider)
    {
        _provider = provider
            ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task<AiTripItineraryResponse> GenerateAsync(
        AiTripItineraryRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Reject invalid traveler input before contacting AI.
        AiTripItineraryValidator.ValidateRequest(request);

        // Provider implementation must run server-side.
        var json = await _provider.GenerateItineraryJsonAsync(
            request,
            cancellationToken);

        // Never trust raw AI output.
        // Validate structure, dates, costs, and review flag.
        return AiTripItineraryJsonParser.ParseAndValidate(
            request,
            json);
    }
}
