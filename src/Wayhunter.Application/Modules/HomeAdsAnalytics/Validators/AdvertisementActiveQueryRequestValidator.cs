
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

        // Date/time is optional, but must be UTC if supplied.
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
        // Only supported placement values are allowed.
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
        if (request.District is not null)
        {
            var district = request.District;

            if (string.IsNullOrWhiteSpace(district))
                return false;

            if (district.Length > 100)
                return false;

            if (district != district.Trim())
                return false;

            // Allow letters, spaces, and hyphens only.
            foreach (char character in district)
            {
                if (!char.IsLetter(character) &&
                    character != ' ' &&
                    character != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }
}
