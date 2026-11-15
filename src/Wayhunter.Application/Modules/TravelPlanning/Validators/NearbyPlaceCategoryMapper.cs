
using System;

namespace Wayhunter.Application.Modules.TravelPlanning.Validators;

public enum NearbyPlaceCategory
{
    Food,
    Hospital,
    Fuel,
    Police,
    Atm,
    Attraction
}

public static class NearbyPlaceCategoryMapper
{
    public static string ToSearchTerm(
        NearbyPlaceCategory category)
    {
        return category switch
        {
            NearbyPlaceCategory.Food => "restaurant",
            NearbyPlaceCategory.Hospital => "hospital",
            NearbyPlaceCategory.Fuel => "fuel station",
            NearbyPlaceCategory.Police => "police station",
            NearbyPlaceCategory.Atm => "ATM",
            NearbyPlaceCategory.Attraction => "tourist attraction",

            _ => throw new ArgumentOutOfRangeException(
                nameof(category),
                "Unsupported nearby category.")
        };
    }

    public static NearbyPlaceCategory FromClientValue(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Nearby category is required.",
                nameof(value));
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "food" => NearbyPlaceCategory.Food,
            "hospital" => NearbyPlaceCategory.Hospital,
            "fuel" => NearbyPlaceCategory.Fuel,
            "police" => NearbyPlaceCategory.Police,
            "atm" => NearbyPlaceCategory.Atm,
            "attraction" => NearbyPlaceCategory.Attraction,

            _ => throw new ArgumentException(
                "Unsupported nearby category.",
                nameof(value))
        };
    }
}
