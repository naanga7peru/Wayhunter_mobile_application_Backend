using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

public sealed record HunterEvidenceBinding(Guid HunterId, Guid SubmissionId, Guid SessionId, HunterEvidenceType Type);
public sealed record HunterUploadAuthorization(Guid UploadId, Uri UploadUrl, DateTimeOffset ExpiresAtUtc);
public sealed record HunterEvidenceCompletion(Guid EvidenceId, HunterEvidenceType Type, DateTimeOffset CapturedAtUtc);

// MEDIA-01 candidates. Begin checks session ownership, expiry, revocation and sequence.
// Associate repeats those checks and atomically records evidence, uniqueness and consumption.
public interface IHunterEvidenceAtomicCandidateStore
{
    Task<bool> CanAuthorizeAsync(HunterEvidenceBinding binding, DateTimeOffset now, CancellationToken ct);
    Task<HunterEvidenceCompletion?> AssociateAsync(
        HunterEvidenceBinding binding, HunterVerifiedUploadReceipt receipt, DateTimeOffset now, CancellationToken ct);
}

public interface IHunterUploadSignerCandidate
{
    Task<HunterUploadAuthorization?> AuthorizeAsync(
        HunterEvidenceBinding binding, string contentType, CancellationToken ct);
}

public sealed class HunterEvidenceWorkflowCandidate(
    HunterCaptureSubmissionOwnershipService ownership,
    HunterCaptureSessionIdentityResolutionService identity,
    HunterEvidenceReceiptCandidate receipts,
    IHunterEvidenceAtomicCandidateStore store,
    IHunterUploadSignerCandidate signer,
    TimeProvider clock)
{
    public async Task<HunterUploadAuthorization?> AuthorizeAsync(
        string userId, Guid submissionId, Guid sessionId, HunterEvidenceType type,
        string contentType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));

        var binding = await BindAsync(userId, submissionId, sessionId, type, ct);
        return await store.CanAuthorizeAsync(binding, clock.GetUtcNow(), ct)
            ? await signer.AuthorizeAsync(binding, contentType, ct) : null;
    }

    public async Task<HunterEvidenceCompletion?> CompleteAsync(
        string userId, Guid submissionId, Guid sessionId, HunterEvidenceType type, Guid uploadId, CancellationToken ct)
    {
        if (uploadId == Guid.Empty)
            throw new ArgumentException("Upload ID is required.", nameof(uploadId));

        var binding = await BindAsync(userId, submissionId, sessionId, type, ct);
        var receipt = await receipts.VerifyBindingAsync(uploadId, binding.HunterId, submissionId, sessionId, type, ct);
        return receipt is null ? null : await store.AssociateAsync(binding, receipt, clock.GetUtcNow(), ct);
    }

    private async Task<HunterEvidenceBinding> BindAsync(
        string userId, Guid submissionId, Guid sessionId, HunterEvidenceType type, CancellationToken ct)
    {
        if (submissionId == Guid.Empty || sessionId == Guid.Empty || !Enum.IsDefined(type))
            throw new ArgumentException("Invalid evidence binding.");

        await ownership.EnsureCanStartCaptureAsync(userId, submissionId, ct);
        var hunterId = await identity.ResolveRequiredAsync(userId, ct);
        return new(hunterId, submissionId, sessionId, type);
    }
}
