
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

public static class AdvertisementStatusUpdateRequestValidator
{
    public static bool IsValid(
        AdvertisementStatusUpdateRequest? request)
    {
        if (request?.Status is null)
            return false;

        return request.Status == AdvertisementStatus.Active
            || request.Status == AdvertisementStatus.Paused
            || request.Status == AdvertisementStatus.Archived;
    }
}
