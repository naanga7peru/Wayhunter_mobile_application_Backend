
using System;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

public static class AdvertisementCreateRequestValidator
{
    public static bool IsValid(
        AdvertisementCreateRequest? request)
    {
        if (request is null)
            return false;

        // Title must contain 1-200 characters.
        if (string.IsNullOrWhiteSpace(request.Title))
            return false;

        if (request.Title.Trim().Length > 200)
            return false;

        // Image URL is required and must be safe.
        if (!AdvertisementUrlValidator.IsValidImageUrl(
                request.ImageUrl))
        {
            return false;
        }

        // Destination URL is optional but must be safe if given.
        if (!AdvertisementUrlValidator.IsValidDestinationUrl(
                request.DestinationUrl))
        {
            return false;
        }

        // Both dates are required and must be UTC.
        if (request.StartAtUtc == default ||
            request.EndAtUtc == default)
        {
            return false;
        }

        // Reject non-UTC offsets such as +05:30.
        if (request.StartAtUtc.Offset != TimeSpan.Zero ||
            request.EndAtUtc.Offset != TimeSpan.Zero)
        {
            return false;
        }

        // End date must be after start date.
        if (request.EndAtUtc <= request.StartAtUtc)
            return false;

        // Only supported advertisement placements.
        if (request.Placement != AdvertisementPlacement.HomeBanner &&
            request.Placement != AdvertisementPlacement.SponsoredPoi)
        {
            return false;
        }

        return true;
    }
}
