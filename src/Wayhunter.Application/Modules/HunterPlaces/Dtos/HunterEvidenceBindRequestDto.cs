using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

public sealed class HunterEvidenceBindRequestDto
{
    public Guid CaptureSessionId { get; set; }
    public HunterEvidenceType EvidenceType { get; set; }
    public string ProviderReceipt { get; set; } = string.Empty;
}

public sealed record VerifiedHunterMediaReceipt(
    string OwnerUserId,
    Guid SubmissionId,
    Guid CaptureSessionId,
    string ProviderAssetId,
    string PrivateMediaReference,
    string ReceiptDigest,
    HunterEvidenceType EvidenceType,
    DateTimeOffset CapturedAtUtc,
    double? CaptureLatitude,
    double? CaptureLongitude,
    double? DurationSeconds);
