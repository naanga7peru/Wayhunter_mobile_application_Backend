using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Queries;

// The source must read confirmed publication and moderation state from an approved catalogue.
public interface IHunterPublishedPlaceCandidateSource
{
    IReadOnlyList<HunterPublicationCandidate> ReadSnapshot();
}

public sealed record HunterPublicationCandidate(
    Guid Id, string Name, string District, string? Description, double Latitude, double Longitude,
    HunterSubmissionStatus Status, bool Published, bool Deactivated);

public sealed record HunterPublicPlaceCandidate(
    Guid Id, string Name, string District, string? Description, double Latitude, double Longitude);

public sealed class HunterDiscoveryCandidate(IHunterPublishedPlaceCandidateSource source)
{
    public IReadOnlyList<HunterPublicPlaceCandidate> Search(
        string? search, string? district, int offset, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return Visible()
            .Where(place => string.IsNullOrWhiteSpace(search) ||
                place.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(place => string.IsNullOrWhiteSpace(district) ||
                string.Equals(place.District, district.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(place => place.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(place => place.Id)
            .Skip(offset)
            .Take(limit)
            .ToArray();
    }

    public HunterPublicPlaceCandidate? Find(Guid id) => Visible().SingleOrDefault(place => place.Id == id);

    public IReadOnlyList<HunterDuplicateCandidate> FindNearbyDuplicateCandidates(
        double latitude, double longitude) =>
        HunterDuplicateCandidateLookup.FindCandidates(latitude, longitude,
            Visible().Select(place => new ExistingVerifiedPlace(
                place.Id, place.Name, place.Latitude, place.Longitude)));

    private IEnumerable<HunterPublicPlaceCandidate> Visible() =>
        source.ReadSnapshot()
            .Where(place => place.Status == HunterSubmissionStatus.Approved &&
                place.Published && !place.Deactivated)
            .Select(place => new HunterPublicPlaceCandidate(
                place.Id, place.Name, place.District, place.Description, place.Latitude, place.Longitude));
}
