
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

/// <summary>
/// Server-side interface for an external AI
/// trip itinerary generation provider.
/// </summary>
public interface IAiTripGenerationProvider
{
    /// <summary>
    /// Generates a proposed itinerary as JSON.
    ///
    /// The backend must validate the JSON before
    /// it can be shown or converted to a trip draft.
    ///
    /// Provider API keys must stay on the server.
    /// </summary>
    Task<string> GenerateItineraryJsonAsync(
        AiTripItineraryRequest request,
        CancellationToken cancellationToken = default);
}
