using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Validators;

public static class HunterDraftRequestValidator
{
    public static IReadOnlyList<string> Validate(
        HunterDraftRequestDto request,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("A UTC validation time is required.", nameof(nowUtc));

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.PlaceName) || request.PlaceName.Trim().Length > 200)
            errors.Add("Place name is required and must be at most 200 characters.");
        if (request.Description?.Trim().Length > 2000)
            errors.Add("Description must be at most 2000 characters.");
        if (string.IsNullOrWhiteSpace(request.ClaimedDistrict) || request.ClaimedDistrict.Trim().Length > 100)
            errors.Add("Claimed district is required and must be at most 100 characters.");
        if (!double.IsFinite(request.Latitude) || request.Latitude is < -90 or > 90)
            errors.Add("Latitude must be finite and between -90 and 90.");
        if (!double.IsFinite(request.Longitude) || request.Longitude is < -180 or > 180)
            errors.Add("Longitude must be finite and between -180 and 180.");
        if (request.GpsAccuracyMeters is double accuracy &&
            (!double.IsFinite(accuracy) || accuracy <= 0))
            errors.Add("GPS accuracy must be finite and greater than zero.");
        if (request.LocationCapturedAtUtc is DateTimeOffset captured &&
            (captured.Offset != TimeSpan.Zero || captured > nowUtc.AddMinutes(5)))
            errors.Add("Location capture time must be UTC and not more than five minutes in the future.");
        return errors;
    }

    public static void Apply(HunterDraftRequestDto request, Domain.Modules.HunterPlaces.Entities.HunterSubmission submission)
    {
        submission.PlaceName = request.PlaceName.Trim();
        submission.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        submission.District = request.ClaimedDistrict.Trim();
        submission.Latitude = request.Latitude;
        submission.Longitude = request.Longitude;
        submission.GpsAccuracyMeters = request.GpsAccuracyMeters;
        submission.LocationCapturedAtUtc = request.LocationCapturedAtUtc;
    }
}
