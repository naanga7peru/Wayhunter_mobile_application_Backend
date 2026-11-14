
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Domain.Modules.HunterPlaces.Entities;

public sealed class HunterEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Related Hunter submission
    public Guid HunterSubmissionId { get; set; }

    // Guided 360 video or live camera photo
    public HunterEvidenceType EvidenceType { get; set; }

    // Protected media reference, not a public URL
    public string PrivateMediaReference { get; set; }
        = string.Empty;

    // Capture information received from device
    public DateTimeOffset CapturedAtUtc { get; set; }

    // Trusted backend upload timestamp
    public DateTimeOffset UploadedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;

    // Optional evidence metadata
    public double? CaptureLatitude { get; set; }
    public double? CaptureLongitude { get; set; }
    public double? DurationSeconds { get; set; }
}
