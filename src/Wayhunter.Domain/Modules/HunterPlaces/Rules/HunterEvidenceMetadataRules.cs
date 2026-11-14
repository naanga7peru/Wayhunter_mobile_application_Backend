
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Domain.Modules.HunterPlaces.Rules;

public static class HunterEvidenceMetadataRules
{
    public static void Validate(HunterEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.HunterSubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission ID is required.",
                nameof(evidence));
        }

        if (!Enum.IsDefined(evidence.EvidenceType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidence.EvidenceType));
        }

        if (string.IsNullOrWhiteSpace(
            evidence.PrivateMediaReference))
        {
            throw new ArgumentException(
                "Private media reference is required.",
                nameof(evidence));
        }

        if (evidence.CapturedAtUtc == default)
        {
            throw new ArgumentException(
                "Capture timestamp is required.",
                nameof(evidence));
        }

        if (evidence.UploadedAtUtc == default)
        {
            throw new ArgumentException(
                "Upload timestamp is required.",
                nameof(evidence));
        }

        // GPS coordinates must be provided together.
        bool hasLatitude = evidence.CaptureLatitude.HasValue;
        bool hasLongitude = evidence.CaptureLongitude.HasValue;

        if (hasLatitude != hasLongitude)
        {
            throw new ArgumentException(
                "Capture latitude and longitude must be " +
                "provided together.",
                nameof(evidence));
        }

        if (evidence.CaptureLatitude is double latitude)
        {
            if (!double.IsFinite(latitude) ||
                latitude < -90 ||
                latitude > 90)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(evidence.CaptureLatitude));
            }
        }

        if (evidence.CaptureLongitude is double longitude)
        {
            if (!double.IsFinite(longitude) ||
                longitude < -180 ||
                longitude > 180)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(evidence.CaptureLongitude));
            }
        }

        // Validate duration if media metadata provides it.
        if (evidence.DurationSeconds is double duration)
        {
            if (!double.IsFinite(duration) || duration <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(evidence.DurationSeconds));
            }
        }
    }
}
