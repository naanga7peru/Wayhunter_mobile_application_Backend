using System;

namespace Wayhunter.Api.Hubs.TripSafety;

public sealed record PublishTripLocationRequest(
    double Latitude,
    double Longitude,
    double AccuracyMeters);

public sealed record TripLocationEventDto(
    Guid TripPlanId,
    Guid SessionId,
    string UserId,
    double Latitude,
    double Longitude,
    double AccuracyMeters,
    DateTimeOffset RecordedAtUtc);

public static class TripLocationInputValidator
{
    public static TripLocationEventDto CreateEvent(
        Guid tripPlanId,
        Guid sessionId,
        string authenticatedUserId,
        PublishTripLocationRequest request,
        DateTimeOffset serverTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid trip and session IDs are required.");
        }

        if (string.IsNullOrWhiteSpace(
            authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        if (!double.IsFinite(request.Latitude) ||
            request.Latitude < -90 ||
            request.Latitude > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Invalid latitude.");
        }

        if (!double.IsFinite(request.Longitude) ||
            request.Longitude < -180 ||
            request.Longitude > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Invalid longitude.");
        }

        if (!double.IsFinite(request.AccuracyMeters) ||
            request.AccuracyMeters < 0 ||
            request.AccuracyMeters > 5000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Invalid GPS accuracy.");
        }

        if (serverTimeUtc == default)
        {
            throw new ArgumentException(
                "Valid server time is required.",
                nameof(serverTimeUtc));
        }

        return new TripLocationEventDto(
            tripPlanId,
            sessionId,
            authenticatedUserId.Trim(),
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters,
            serverTimeUtc.ToUniversalTime());
    }
}
