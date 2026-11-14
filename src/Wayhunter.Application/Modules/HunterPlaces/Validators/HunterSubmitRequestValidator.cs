
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Validation;

public static class HunterSubmitRequestValidator
{
    public static IReadOnlyList<string> Validate(
        HunterSubmitRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        // Place name is required.
        if (string.IsNullOrWhiteSpace(request.PlaceName))
        {
            errors.Add("Place name is required.");
        }

        // Claimed district must be supplied.
        // The server must independently verify the district.
        if (string.IsNullOrWhiteSpace(request.ClaimedDistrict))
        {
            errors.Add("District is required.");
        }

        // Latitude must be between -90 and 90.
        if (!double.IsFinite(request.Latitude) ||
            request.Latitude < -90 ||
            request.Latitude > 90)
        {
            errors.Add("Invalid GPS latitude.");
        }

        // Longitude must be between -180 and 180.
        if (!double.IsFinite(request.Longitude) ||
            request.Longitude < -180 ||
            request.Longitude > 180)
        {
            errors.Add("Invalid GPS longitude.");
        }

        // If GPS accuracy is supplied, it must be positive.
        if (request.GpsAccuracyMeters is double accuracy &&
            (!double.IsFinite(accuracy) || accuracy <= 0))
        {
            errors.Add("Invalid GPS accuracy.");
        }

        // 360-degree video evidence reference is required.
        if (request.Guided360VideoEvidenceId == Guid.Empty)
        {
            errors.Add("360-degree video evidence is required.");
        }

        // Live photo evidence reference is required.
        if (request.LivePhotoEvidenceId == Guid.Empty)
        {
            errors.Add("Live photo evidence is required.");
        }

        // Video and photo cannot reference the same evidence.
        if (request.Guided360VideoEvidenceId != Guid.Empty &&
            request.Guided360VideoEvidenceId ==
                request.LivePhotoEvidenceId)
        {
            errors.Add("Video and photo evidence must be different.");
        }

        return errors;
    }
}
