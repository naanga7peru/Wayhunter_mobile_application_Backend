using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

public interface IHunterDraftStore
{
    Task<HunterSubmission> CreateAsync(HunterSubmission submission, string keyHash, string requestDigest, CancellationToken cancellationToken = default);
    Task<HunterDraftUpdateResult> UpdateDraftAsync(Guid submissionId, string ownerUserId, HunterDraftRequestDto request, CancellationToken cancellationToken = default);
    Task<HunterDraftDeleteResult> DeleteDraftAsync(Guid submissionId, string ownerUserId, CancellationToken cancellationToken = default);
}

public enum HunterDraftDeleteResult { Deleted, NotFound, Conflict }
public enum HunterDraftUpdateResult { Updated, NotFound, Conflict }
