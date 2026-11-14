
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

public interface IHunterSubmissionService
{
    // Submit a Hunter place for Admin verification.
    Task<HunterSubmissionResponseDto> SubmitAsync(
        string authenticatedUserId,
        HunterSubmitRequestDto request,
        CancellationToken cancellationToken = default
    );

    // Fetch a submission belonging to the authenticated Hunter.
    Task<HunterSubmissionResponseDto?> GetMySubmissionAsync(
        string authenticatedUserId,
        Guid submissionId,
        CancellationToken cancellationToken = default
    );
}
