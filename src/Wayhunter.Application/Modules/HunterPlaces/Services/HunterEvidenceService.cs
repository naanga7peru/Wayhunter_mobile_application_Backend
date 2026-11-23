using System.Security.Cryptography;
using System.Text;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

public sealed class HunterMediaUnavailableException : Exception
{
    public HunterMediaUnavailableException() : base("Trusted Hunter media verification is not configured.") { }
}

public sealed class HunterEvidenceConflictException(string message) : Exception(message);

public sealed class HunterEvidenceService
{
    private readonly IHunterMediaReceiptVerifier _verifier;
    private readonly IHunterEvidenceStore _store;
    private readonly TimeProvider _clock;

    public HunterEvidenceService(IHunterMediaReceiptVerifier verifier, IHunterEvidenceStore store, TimeProvider clock)
    { _verifier = verifier; _store = store; _clock = clock; }

    public bool IsProviderConfigured => _verifier.IsConfigured;

    public async Task<HunterEvidenceBindResult> BindAsync(string ownerUserId, Guid submissionId,
        HunterEvidenceBindRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(ownerUserId, "D", out var owner) || owner == Guid.Empty || submissionId == Guid.Empty)
            throw new UnauthorizedAccessException("Trusted owner identity is required.");
        if (request is null || request.CaptureSessionId == Guid.Empty || !Enum.IsDefined(request.EvidenceType) ||
            string.IsNullOrWhiteSpace(request.ProviderReceipt) || request.ProviderReceipt.Length > 4096)
            throw new ArgumentException("A capture session, evidence type and bounded provider receipt are required.", nameof(request));

        if (!_verifier.IsConfigured) throw new HunterMediaUnavailableException();
        var verified = await _verifier.VerifyAsync(request.ProviderReceipt, ownerUserId,
            submissionId, request.CaptureSessionId, cancellationToken);
        if (verified is null) throw new HunterMediaUnavailableException();
        var now = _clock.GetUtcNow();
        if (!string.Equals(verified.OwnerUserId, ownerUserId, StringComparison.Ordinal) ||
            verified.SubmissionId != submissionId || verified.CaptureSessionId != request.CaptureSessionId ||
            !Enum.IsDefined(verified.EvidenceType) || verified.EvidenceType != request.EvidenceType ||
            string.IsNullOrWhiteSpace(verified.ProviderAssetId) || verified.ProviderAssetId.Length > 512 ||
            string.IsNullOrWhiteSpace(verified.PrivateMediaReference) || verified.PrivateMediaReference.Length > 2048 ||
            string.IsNullOrWhiteSpace(verified.ReceiptDigest) || verified.ReceiptDigest.Length > 512 ||
            verified.CapturedAtUtc == default || verified.CapturedAtUtc.Offset != TimeSpan.Zero ||
            verified.CapturedAtUtc > now ||
            (verified.CaptureLatitude.HasValue != verified.CaptureLongitude.HasValue) ||
            (verified.CaptureLatitude is double lat && (!double.IsFinite(lat) || lat is < -90 or > 90)) ||
            (verified.CaptureLongitude is double lon && (!double.IsFinite(lon) || lon is < -180 or > 180)) ||
            (verified.DurationSeconds is double duration && (!double.IsFinite(duration) || duration <= 0)))
            throw new ArgumentException("Provider verification returned invalid or mismatched media facts.");

        var assetHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(verified.ProviderAssetId)));
        var receiptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(verified.ReceiptDigest)));
        var result = await _store.BindAsync(submissionId, request.CaptureSessionId, ownerUserId,
            owner, verified with { ProviderAssetId = assetHash, ReceiptDigest = receiptHash }, now, cancellationToken);
        if (!result.Bound) throw new HunterEvidenceConflictException("Evidence or capture session is already used or ineligible.");
        return result;
    }
}
