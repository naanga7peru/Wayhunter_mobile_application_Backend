using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>
/// Atomically writes a review decision, updates its pending submission,
/// and revokes unused capture sessions. A null result means the
/// submission is missing or no longer PendingReview.
/// </summary>
public interface IHunterReviewStore
{
    Task<HunterSubmissionResponseDto?> TryReviewPendingAsync(
        HunterReviewDecision decision,
        CancellationToken cancellationToken = default);
}
