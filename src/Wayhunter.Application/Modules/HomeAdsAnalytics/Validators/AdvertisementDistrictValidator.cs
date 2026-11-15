
using System;
using System.Collections.Generic;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

public static class AdvertisementDistrictValidator
{
    private static readonly HashSet<string> ValidDistricts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Ampara",
            "Anuradhapura",
            "Badulla",
            "Batticaloa",
            "Colombo",
            "Galle",
            "Gampaha",
            "Hambantota",
            "Jaffna",
            "Kalutara",
            "Kandy",
            "Kegalle",
            "Kilinochchi",
            "Kurunegala",
            "Mannar",
            "Matale",
            "Matara",
            "Monaragala",
            "Mullaitivu",
            "Nuwara Eliya",
            "Polonnaruwa",
            "Puttalam",
            "Ratnapura",
            "Trincomalee",
            "Vavuniya"
        };

    public static bool IsValid(string? district)
    {
        if (string.IsNullOrWhiteSpace(district))
            return false;

        if (district != district.Trim())
            return false;

        return ValidDistricts.Contains(district);
    }
}
