
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

public interface IHunterReviewService
{
    // Admin reviews a Hunter place submission.
    Task<HunterSubmissionResponseDto> ReviewAsync(
        string authenticatedAdminUserId,
        HunterReviewRequestDto request,
        CancellationToken cancellationToken = default
    );
}
