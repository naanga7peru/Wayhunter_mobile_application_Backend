
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

public static class AdvertisementUpdateRequestValidator
{
    public static bool IsValid(
        AdvertisementUpdateRequest? request)
    {
        if (request is null)
            return false;

        // Reuse the existing Create Request validation rules.
        var createRequest = new AdvertisementCreateRequest
        {
            Title = request.Title,
            ImageUrl = request.ImageUrl,
            DestinationUrl = request.DestinationUrl,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            Placement = request.Placement
        };

        return AdvertisementCreateRequestValidator.IsValid(
            createRequest);
    }
}
