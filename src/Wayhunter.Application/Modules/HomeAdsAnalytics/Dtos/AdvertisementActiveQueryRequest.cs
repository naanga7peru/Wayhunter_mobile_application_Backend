
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;

public sealed record AdvertisementActiveQueryRequest
{
    // Optional date/time for active advertisement lookup.
    public DateTimeOffset? AtUtc { get; init; }

    // Optional placement: HomeBanner or SponsoredPoi.
    public AdvertisementPlacement? Placement { get; init; }

    // Optional district targeting filter.
    public string? District { get; init; }
}
