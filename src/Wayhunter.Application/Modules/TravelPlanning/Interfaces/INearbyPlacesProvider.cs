
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

/// <summary>
/// Contract for location-based nearby place searches.
/// Implementation belongs to Infrastructure.
/// </summary>
public interface INearbyPlacesProvider
{
    /// <summary>
    /// Search for nearby places using GPS coordinates,
    /// a fixed search radius and provider search term.
    ///
    /// Results are external suggestions only.
    /// They are NOT automatically Hunter-approved.
    /// </summary>
    Task<PlacesProviderResult> SearchNearbyAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        string searchTerm,
        CancellationToken cancellationToken = default);
}
