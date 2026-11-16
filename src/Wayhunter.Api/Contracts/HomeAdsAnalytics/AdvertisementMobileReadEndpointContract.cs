
namespace Wayhunter.Api.Contracts.HomeAdsAnalytics;

// M7-MS02-T03
// Proposed mobile advertisement read contract.
//
// This class only defines metadata.
// It does not register or expose an HTTP endpoint.

public static class AdvertisementMobileReadEndpointContract
{
    public const string ActiveAdsRoute =
        "api/mobile/advertisements/active";

    public const string HttpMethod = "GET";

    public const string PlacementParameter = "placement";

    // District-targeted advertisements require
    // the approved SQL persistence integration.
    public const bool DistrictTargetingReady = false;

    // Mobile clients cannot select arbitrary
    // advertisement query timestamps.
    public const bool ClientSuppliedTimeSupported = false;

    public static bool IsSupportedPlacement(
        string? placement)
    {
        return placement is "HomeBanner" or "SponsoredPoi";
    }
}
