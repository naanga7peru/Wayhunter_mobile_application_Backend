using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>
/// Reads a submission and its owner from authoritative storage.
/// </summary>
public interface IHunterSubmissionStatusReader
{
    Task<HunterSubmissionStatusSnapshot?> FindByIdAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HunterSubmissionStatusSnapshot>> FindMineAsync(
        string ownerUserId,
        bool approvedOnly = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovedHunterPlaceDto>> FindApprovedPlacesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HunterSubmissionStatusSnapshot>> FindPendingReviewAsync(
        CancellationToken cancellationToken = default);
}
