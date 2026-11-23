using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

public interface IHunterEvidenceStore
{
    Task<HunterEvidenceBindResult> BindAsync(Guid submissionId, Guid sessionId,
        string ownerUserId, Guid hunterId, VerifiedHunterMediaReceipt receipt,
        DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> HasCompleteVerifiedEvidenceAsync(Guid submissionId, string ownerUserId,
        CancellationToken cancellationToken = default);
}

public sealed record HunterEvidenceBindResult(bool Bound, bool Replayed, Guid EvidenceId);
