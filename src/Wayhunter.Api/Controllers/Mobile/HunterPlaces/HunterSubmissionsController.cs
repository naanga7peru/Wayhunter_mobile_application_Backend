using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Services;

namespace Wayhunter.Api.Controllers.Mobile.HunterPlaces;

[ApiController]
[Authorize]
[Route("api/mobile/hunter/submissions")]
public sealed class HunterSubmissionsController : ControllerBase
{
    private readonly HunterSubmissionStatusService _status;
    private readonly ITrustedIdentityAccountReader _accounts;

    public HunterSubmissionsController(
        HunterSubmissionStatusService status,
        ITrustedIdentityAccountReader accounts)
    {
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
    }

    [HttpGet("{submissionId:guid}")]
    [ProducesResponseType<HunterSubmissionStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetSubmission(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User, out var userId))
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "authentication_required",
                "Authentication is required.");
        }

        try
        {
            var account = await _accounts.FindByUserIdAsync(
                userId, cancellationToken);

            if (!TrustedIdentityAccessContract.IsActiveAccountOwner(
                    userId, account))
            {
                return Error(
                    StatusCodes.Status403Forbidden,
                    "access_denied",
                    "The authenticated account is not allowed to access this resource.");
            }

            var submission = await _status.GetMySubmissionAsync(
                userId, submissionId, cancellationToken);

            // Missing and foreign submissions intentionally share the same
            // empty 404 response so ownership is not disclosed.
            return submission is null
                ? Error(
                    StatusCodes.Status404NotFound,
                    "hunter_submission_not_found",
                    "The requested submission was not found.")
                : Ok(new HunterSubmissionStatusResponse(
                    submission.PlaceName,
                    submission.District,
                    submission.Status,
                    submission.CreatedAtUtc,
                    submission.SubmittedAtUtc));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Error(
                StatusCodes.Status503ServiceUnavailable,
                "hunter_submission_unavailable",
                "The submission could not be retrieved.");
        }
    }

    private ObjectResult Error(int status, string code, string message) =>
        StatusCode(status, ApiErrorResponse.Create(
            code, message, HttpContext.TraceIdentifier));
}
