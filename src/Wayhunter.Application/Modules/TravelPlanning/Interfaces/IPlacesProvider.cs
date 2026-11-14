
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

// Contract for external place search.
// Hunter-approved places are owned by M4.
public interface IPlacesProvider
{
    Task<PlacesProviderResult> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}

// External search result.
// It must not automatically be treated as a verified place.
public sealed record ExternalPlaceSuggestion(
    string ProviderPlaceId,
    string Name,
    double Latitude,
    double Longitude,
    string ProviderName
);

// Explicit result status for provider failures.
public sealed record PlacesProviderResult(
    bool IsSuccess,
    IReadOnlyList<ExternalPlaceSuggestion> Places,
    string? FailureReason
);
