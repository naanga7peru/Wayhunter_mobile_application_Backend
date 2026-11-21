using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>Reads submissions for the user ID supplied by trusted authentication.</summary>
public interface IHunterOwnedSubmissionReader
{
    Task<HunterSubmissionResponseDto?> GetMySubmissionAsync(
        string authenticatedUserId, Guid submissionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HunterSubmissionResponseDto>> ListMySubmissionsAsync(
        string authenticatedUserId, CancellationToken cancellationToken = default);

    Task<int> CountMyApprovedHuntsAsync(
        string authenticatedUserId, CancellationToken cancellationToken = default);
}
