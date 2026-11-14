
using System;

namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

public sealed class HunterSubmitRequestDto
{
    // Proposed place
    public string PlaceName { get; set; } = string.Empty;
    public string? Description { get; set; }

    // GPS details supplied by the device
    public string ClaimedDistrict { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? GpsAccuracyMeters { get; set; }
    public DateTimeOffset? LocationCapturedAtUtc { get; set; }

    // References to evidence already received by the backend
    public Guid Guided360VideoEvidenceId { get; set; }
    public Guid LivePhotoEvidenceId { get; set; }
}
