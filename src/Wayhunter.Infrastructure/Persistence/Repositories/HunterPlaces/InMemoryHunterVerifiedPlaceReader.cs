
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

public sealed class InMemoryHunterVerifiedPlaceReader
    : IHunterVerifiedPlaceReader
{
    private readonly IReadOnlyList<ExistingVerifiedPlace> _places;

    public InMemoryHunterVerifiedPlaceReader(
        IEnumerable<ExistingVerifiedPlace> verifiedPlaces)
    {
        ArgumentNullException.ThrowIfNull(verifiedPlaces);

        // Store a snapshot of supplied verified places.
        _places = verifiedPlaces.ToArray();
    }

    public Task<IReadOnlyList<ExistingVerifiedPlace>>
        GetVerifiedPlacesNearAsync(
            double latitude,
            double longitude,
            double radiusMeters,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!double.IsFinite(radiusMeters) || radiusMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusMeters));
        }

        // Validate new GPS coordinates even when no places exist.
        HunterDuplicateCandidateRules.CalculateDistanceMeters(
            latitude, longitude, latitude, longitude);

        var nearbyPlaces = new List<ExistingVerifiedPlace>();

        foreach (var place in _places)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double distance =
                HunterDuplicateCandidateRules.CalculateDistanceMeters(
                    latitude,
                    longitude,
                    place.Latitude,
                    place.Longitude);

            if (distance <= radiusMeters)
            {
                nearbyPlaces.Add(place);
            }
        }

        return Task.FromResult<IReadOnlyList<ExistingVerifiedPlace>>(
            nearbyPlaces);
    }
}
