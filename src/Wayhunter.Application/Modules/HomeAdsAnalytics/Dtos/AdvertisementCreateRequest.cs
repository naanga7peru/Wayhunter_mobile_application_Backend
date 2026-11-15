
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;

public sealed record AdvertisementCreateRequest
{
    public string Title { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public string? DestinationUrl { get; init; }

    public DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset EndAtUtc { get; init; }

    public AdvertisementPlacement Placement { get; init; }
        = AdvertisementPlacement.Unspecified;
}
