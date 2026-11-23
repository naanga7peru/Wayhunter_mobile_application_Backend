using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Services;

namespace Wayhunter.Api.Controllers.Mobile.HunterPlaces;

[ApiController]
[Authorize]
[Route("api/mobile/hunter/places")]
public sealed class ApprovedHunterPlacesController : ControllerBase
{
    private readonly HunterSubmissionStatusService _submissions;
    private readonly ITrustedIdentityAccountReader _accounts;

    public ApprovedHunterPlacesController(
        HunterSubmissionStatusService submissions,
        ITrustedIdentityAccountReader accounts)
    {
        _submissions = submissions ?? throw new ArgumentNullException(nameof(submissions));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApprovedHunterPlaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return StatusCode(StatusCodes.Status401Unauthorized, ApiErrorResponse.Create(
                "authentication_required", "Authentication is required.", HttpContext.TraceIdentifier));
        try
        {
            var account = await _accounts.FindByUserIdAsync(userId, cancellationToken);
            if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account))
                return StatusCode(StatusCodes.Status401Unauthorized, ApiErrorResponse.Create(
                    "authentication_required", "Authentication is required.", HttpContext.TraceIdentifier));
            return Ok(await _submissions.GetApprovedPlacesAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiErrorResponse.Create(
                "hunter_places_unavailable", "Approved places could not be retrieved.", HttpContext.TraceIdentifier));
        }
    }
}
