
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Domain.Modules.HunterPlaces.Entities;

public sealed class HunterSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Hunter details
    public string UserId { get; set; } = string.Empty;
    public string UserNameSnapshot { get; set; } = string.Empty;

    // Place details
    public string PlaceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string District { get; set; } = string.Empty;

    // GPS information
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? GpsAccuracyMeters { get; set; }
    public DateTimeOffset? LocationCapturedAtUtc { get; set; }

    // Submission details
    public HunterSubmissionStatus Status { get; set; }
        = HunterSubmissionStatus.Draft;

    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? SubmittedAtUtc { get; set; }
}
