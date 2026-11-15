
using System;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

public static class AdvertisementActiveQueryRequestValidator
{
    public static bool IsValid(
        AdvertisementActiveQueryRequest? request)
    {
        if (request is null)
            return false;

        // Date/time is optional, but must be UTC.
        if (request.AtUtc.HasValue)
        {
            var atUtc = request.AtUtc.Value;

            if (atUtc == default ||
                atUtc.Offset != TimeSpan.Zero)
            {
                return false;
            }
        }

        // Placement is optional.
        if (request.Placement.HasValue)
        {
            var placement = request.Placement.Value;

            if (placement != AdvertisementPlacement.HomeBanner &&
                placement != AdvertisementPlacement.SponsoredPoi)
            {
                return false;
            }
        }

        // District is optional.
        // If supplied, it must be a valid Sri Lankan district.
        if (request.District is not null)
        {
            if (!AdvertisementDistrictValidator.IsValid(
                    request.District))
            {
                return false;
            }
        }

        return true;
    }
}
