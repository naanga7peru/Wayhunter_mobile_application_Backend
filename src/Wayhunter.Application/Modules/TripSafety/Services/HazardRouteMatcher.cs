
using System;
using System.Collections.Generic;

using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public static class HazardRouteMatcher
{
    public const double ThresholdMeters = 1000.0;

    private const double EarthRadiusMeters = 6371000.0;

    public static bool IsWithinOneKilometer(
        IReadOnlyList<RouteWaypoint> routeGeometry,
        double hazardLatitude,
        double hazardLongitude)
    {
        if (routeGeometry is null ||
            routeGeometry.Count < 2 ||
            !IsValid(hazardLatitude, hazardLongitude))
        {
            return false;
        }

        for (var index = 1; index < routeGeometry.Count; index++)
        {
            var start = routeGeometry[index - 1];
            var end = routeGeometry[index];

            if (start is null || end is null || !IsValid(start.Latitude, start.Longitude) ||
                !IsValid(end.Latitude, end.Longitude))
            {
                continue;
            }

            var distance = DistanceToSegmentMeters(
                hazardLatitude,
                hazardLongitude,
                start.Latitude,
                start.Longitude,
                end.Latitude,
                end.Longitude);

            if (distance <= ThresholdMeters)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsValid(
        double latitude,
        double longitude)
    {
        return double.IsFinite(latitude) &&
               double.IsFinite(longitude) &&
               latitude >= -90 &&
               latitude <= 90 &&
               longitude >= -180 &&
               longitude <= 180;
    }

    internal static double DistanceBetweenMeters(
        double latitude, double longitude, double otherLatitude, double otherLongitude)
    {
        var halfLatitude = DegreesToRadians(otherLatitude - latitude) / 2;
        var halfLongitude = DegreesToRadians(LongitudeDelta(otherLongitude, longitude)) / 2;
        var haversine = Math.Pow(Math.Sin(halfLatitude), 2) +
            Math.Cos(DegreesToRadians(latitude)) * Math.Cos(DegreesToRadians(otherLatitude)) *
            Math.Pow(Math.Sin(halfLongitude), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0, 1)));
    }

    private static double DistanceToSegmentMeters(
        double pointLatitude,
        double pointLongitude,
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude)
    {
        var latitudeRad = DegreesToRadians(pointLatitude);

        var longitudeScale =
            EarthRadiusMeters * Math.Cos(latitudeRad);

        // Unwrap each segment as a unit. Wrapping its endpoints independently can
        // turn a short antimeridian crossing into a line through Greenwich.
        var delta = LongitudeDelta(endLongitude, startLongitude);
        var midpointOffset = LongitudeDelta(startLongitude + delta / 2, pointLongitude);
        var startX =
            DegreesToRadians(midpointOffset - delta / 2)
            * longitudeScale;

        var startY =
            DegreesToRadians(startLatitude - pointLatitude)
            * EarthRadiusMeters;

        var endX =
            DegreesToRadians(midpointOffset + delta / 2)
            * longitudeScale;

        var endY =
            DegreesToRadians(endLatitude - pointLatitude)
            * EarthRadiusMeters;

        var dx = endX - startX;
        var dy = endY - startY;

        var segmentLengthSquared = dx * dx + dy * dy;

        if (segmentLengthSquared <= 0)
        {
            return Math.Sqrt(startX * startX + startY * startY);
        }

        // The hazard is the origin (0,0).
        // Project it onto the route segment.
        var t = -(startX * dx + startY * dy) /
                segmentLengthSquared;

        t = Math.Clamp(t, 0.0, 1.0);

        var closestX = startX + t * dx;
        var closestY = startY + t * dy;

        return Math.Sqrt(
            closestX * closestX +
            closestY * closestY);
    }

    private static double LongitudeDelta(
        double longitude,
        double originLongitude)
    {
        var delta = longitude - originLongitude;

        while (delta > 180)
            delta -= 360;

        while (delta < -180)
            delta += 360;

        return delta;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
