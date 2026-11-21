using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

// Provisional MEDIA-01 verification boundary. Only a backend provider can supply a receipt.
public interface IHunterUploadReceiptVerifier
{
    Task<HunterVerifiedUploadReceipt?> VerifyAsync(Guid uploadId, CancellationToken cancellationToken);
}

public sealed record HunterVerifiedUploadReceipt(
    Guid UploadId, Guid HunterId, Guid SubmissionId, Guid CaptureSessionId,
    string CanonicalMediaId, HunterEvidence Evidence);

public sealed class HunterEvidenceReceiptCandidate(IHunterUploadReceiptVerifier verifier)
{
    public async Task<HunterVerifiedUploadReceipt?> VerifyBindingAsync(
        Guid uploadId, Guid hunterId, Guid submissionId, Guid captureSessionId,
        HunterEvidenceType expectedType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (uploadId == Guid.Empty || hunterId == Guid.Empty || submissionId == Guid.Empty ||
            captureSessionId == Guid.Empty || !Enum.IsDefined(expectedType))
            return null;

        var receipt = await verifier.VerifyAsync(uploadId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (receipt is null || receipt.UploadId != uploadId || receipt.HunterId != hunterId ||
            receipt.SubmissionId != submissionId || receipt.CaptureSessionId != captureSessionId ||
            string.IsNullOrWhiteSpace(receipt.CanonicalMediaId) ||
            receipt.Evidence.HunterSubmissionId != submissionId ||
            receipt.Evidence.EvidenceType != expectedType ||
            receipt.Evidence.CapturedAtUtc > receipt.Evidence.UploadedAtUtc)
        {
            return null;
        }

        HunterEvidenceMetadataRules.Validate(receipt.Evidence);
        return receipt;
    }
}
