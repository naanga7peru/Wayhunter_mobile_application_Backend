using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

public sealed record HunterVerifiedSubmitInputs(
    string OwnerName, string CanonicalDistrict, HunterEvidence Video, HunterEvidence Photo);

// DATA-01 candidate. Load must verify authoritative location and durable proof ownership.
// Commit must recheck Draft, inputs and owner, reserve the replay key, write PendingReview
// and revoke unused sessions atomically. Matching retries return the previously committed response.
public interface IHunterSubmitAtomicCandidateStore
{
    Task<HunterSubmissionResponseDto?> FindReplayAsync(
        string ownerId, Guid submissionId, string operationKey, HunterSubmitRequestDto request, CancellationToken ct);
    Task<HunterVerifiedSubmitInputs?> ReadVerifiedInputsAsync(
        string ownerId, Guid submissionId, HunterSubmitRequestDto request, CancellationToken ct);
    Task<HunterSubmissionResponseDto?> CommitAsync(
        string ownerId, Guid submissionId, string operationKey, HunterSubmitRequestDto request,
        HunterVerifiedSubmitInputs inputs, IReadOnlyList<HunterDuplicateCandidate> candidates, CancellationToken ct);
}

public sealed class HunterSubmitWorkflowCandidate(
    HunterCaptureSubmissionOwnershipService ownership,
    HunterCaptureSessionIdentityResolutionService identity,
    HunterDuplicateCandidateQueryService duplicates,
    IHunterSubmitAtomicCandidateStore store)
{
    public async Task<HunterSubmissionResponseDto?> SubmitAsync(
        string userId, Guid submissionId, string operationKey, HunterSubmitRequestDto request, CancellationToken ct)
    {
        if (submissionId == Guid.Empty || string.IsNullOrWhiteSpace(operationKey) ||
            HunterSubmitRequestValidator.Validate(request).Count != 0)
            throw new ArgumentException("Invalid Hunter submission request.");

        await ownership.EnsureOwnedAsync(userId, submissionId, ct);
        await identity.ResolveRequiredAsync(userId, ct);
        var replay = await store.FindReplayAsync(userId, submissionId, operationKey, request, ct);
        if (replay is not null)
            return replay;

        await ownership.EnsureCanStartCaptureAsync(userId, submissionId, ct);
        var inputs = await store.ReadVerifiedInputsAsync(userId, submissionId, request, ct);
        if (inputs is null || string.IsNullOrWhiteSpace(inputs.OwnerName) ||
            string.IsNullOrWhiteSpace(inputs.CanonicalDistrict) ||
            !string.Equals(inputs.CanonicalDistrict, request.ClaimedDistrict.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;

        HunterSubmissionEvidenceValidator.EnsureReady(submissionId, request, inputs.Video, inputs.Photo);
        var candidates = await duplicates.FindNearbyCandidatesAsync(request.Latitude, request.Longitude, ct);
        return await store.CommitAsync(userId, submissionId, operationKey, request, inputs, candidates, ct);
    }
}
