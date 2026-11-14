
using System;

namespace Wayhunter.Domain.Modules.HunterPlaces.Rules;

public static class HunterDuplicateCandidateRules
{
    public const double CandidateRadiusMeters = 200.0;

    private const double EarthRadiusMeters = 6371000.0;

    public static double CalculateDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        ValidateCoordinates(latitude1, longitude1);
        ValidateCoordinates(latitude2, longitude2);

        double lat1 = ToRadians(latitude1);
        double lat2 = ToRadians(latitude2);

        double deltaLat = ToRadians(latitude2 - latitude1);
        double deltaLon = ToRadians(longitude2 - longitude1);

        double a =
            Math.Pow(Math.Sin(deltaLat / 2), 2) +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Pow(Math.Sin(deltaLon / 2), 2);

        a = Math.Clamp(a, 0.0, 1.0);

        return 2 * EarthRadiusMeters *
               Math.Asin(Math.Sqrt(a));
    }

    public static bool IsPotentialDuplicate(
        double newLatitude,
        double newLongitude,
        double existingLatitude,
        double existingLongitude)
    {
        double distance = CalculateDistanceMeters(
            newLatitude,
            newLongitude,
            existingLatitude,
            existingLongitude);

        return distance <= CandidateRadiusMeters;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static void ValidateCoordinates(
        double latitude,
        double longitude)
    {
        if (!double.IsFinite(latitude) ||
            latitude < -90 ||
            latitude > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude));
        }

        if (!double.IsFinite(longitude) ||
            longitude < -180 ||
            longitude > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude));
        }
    }
}
