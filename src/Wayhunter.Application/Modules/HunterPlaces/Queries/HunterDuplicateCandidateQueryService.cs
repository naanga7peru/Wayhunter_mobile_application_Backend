
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;

namespace Wayhunter.Application.Modules.HunterPlaces.Queries;

public sealed class HunterDuplicateCandidateQueryService
{
    private readonly IHunterVerifiedPlaceReader _placeReader;

    public HunterDuplicateCandidateQueryService(
        IHunterVerifiedPlaceReader placeReader)
    {
        _placeReader = placeReader
            ?? throw new ArgumentNullException(nameof(placeReader));
    }

    public async Task<IReadOnlyList<HunterDuplicateCandidate>>
        FindNearbyCandidatesAsync(
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Validate GPS before querying the repository.
        HunterDuplicateCandidateRules.CalculateDistanceMeters(
            latitude, longitude, latitude, longitude);

        // Read verified places within the 200m radius.
        var existingPlaces =
            await _placeReader.GetVerifiedPlacesNearAsync(
                latitude,
                longitude,
                HunterDuplicateCandidateRules.CandidateRadiusMeters,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Perform exact distance check and nearest-first sorting.
        return HunterDuplicateCandidateLookup.FindCandidates(
            latitude,
            longitude,
            existingPlaces);
    }
}
