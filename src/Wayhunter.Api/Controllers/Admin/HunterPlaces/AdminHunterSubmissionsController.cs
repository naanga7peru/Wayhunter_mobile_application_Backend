using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Authorization;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Services;

namespace Wayhunter.Api.Controllers.Admin.HunterPlaces;

[ApiController]
[Authorize(Policy = CoinsWalletAdministratorPolicy.Name)]
[Route("api/admin/hunter/submissions")]
public sealed class AdminHunterSubmissionsController : ControllerBase
{
    private readonly HunterSubmissionStatusService _submissions;

    public AdminHunterSubmissionsController(HunterSubmissionStatusService submissions) =>
        _submissions = submissions ?? throw new ArgumentNullException(nameof(submissions));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HunterSubmissionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _submissions.GetPendingReviewAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiErrorResponse.Create(
                "hunter_review_queue_unavailable", "Pending submissions could not be retrieved.", HttpContext.TraceIdentifier));
        }
    }
}
