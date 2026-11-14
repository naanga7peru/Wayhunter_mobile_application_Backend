
using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;

namespace Wayhunter.Application.Modules.HunterPlaces.Queries;

// Existing verified place supplied by a repository.
public sealed record ExistingVerifiedPlace(
    Guid PlaceId,
    string PlaceName,
    double Latitude,
    double Longitude
);

// Potential duplicate returned for further review.
public sealed record HunterDuplicateCandidate(
    Guid PlaceId,
    string PlaceName,
    double DistanceMeters
);

public static class HunterDuplicateCandidateLookup
{
    public static IReadOnlyList<HunterDuplicateCandidate> FindCandidates(
        double newLatitude,
        double newLongitude,
        IEnumerable<ExistingVerifiedPlace> existingPlaces)
    {
        ArgumentNullException.ThrowIfNull(existingPlaces);

        // Validate new coordinates even if the list is empty.
        HunterDuplicateCandidateRules.CalculateDistanceMeters(
            newLatitude,
            newLongitude,
            newLatitude,
            newLongitude);

        var candidates = new List<HunterDuplicateCandidate>();

        foreach (var place in existingPlaces)
        {
            double distance =
                HunterDuplicateCandidateRules.CalculateDistanceMeters(
                    newLatitude,
                    newLongitude,
                    place.Latitude,
                    place.Longitude);

            if (distance <=
                HunterDuplicateCandidateRules.CandidateRadiusMeters)
            {
                candidates.Add(new HunterDuplicateCandidate(
                    place.PlaceId,
                    place.PlaceName,
                    distance));
            }
        }

        // Nearest potential duplicates first.
        return candidates
            .OrderBy(candidate => candidate.DistanceMeters)
            .ToArray();
    }
}
