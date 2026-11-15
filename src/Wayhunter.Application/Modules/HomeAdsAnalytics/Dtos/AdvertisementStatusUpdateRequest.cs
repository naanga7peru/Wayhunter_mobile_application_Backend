
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;

public sealed record AdvertisementStatusUpdateRequest
{
    // Required status value:
    // Active, Paused or Archived
    public AdvertisementStatus? Status { get; init; }
}
