using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Api.Controllers.Mobile.HunterPlaces;

[ApiController]
[Authorize]
[Route("api/mobile/hunter/submissions")]
public sealed class HunterSubmissionsController : ControllerBase
{
    private readonly HunterSubmissionStatusService _status;
    private readonly ITrustedIdentityAccountReader _accounts;
    private readonly IHunterCaptureSubmissionAccessReader _captureAccess;
    private readonly HunterStartAuthorizedCaptureSessionHandler _startCapture;

    public HunterSubmissionsController(
        HunterSubmissionStatusService status,
        ITrustedIdentityAccountReader accounts,
        IHunterCaptureSubmissionAccessReader captureAccess,
        HunterStartAuthorizedCaptureSessionHandler startCapture)
    {
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _captureAccess = captureAccess ?? throw new ArgumentNullException(nameof(captureAccess));
        _startCapture = startCapture ?? throw new ArgumentNullException(nameof(startCapture));
    }

    [HttpPost("{submissionId:guid}/capture-sessions")]
    [ProducesResponseType<HunterStartCaptureSessionResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartCaptureSession(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
        {
            return Error(StatusCodes.Status401Unauthorized,
                "authentication_required", "Authentication is required.");
        }

        try
        {
            var account = await _accounts.FindByUserIdAsync(userId, cancellationToken);
            if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account))
            {
                return Error(StatusCodes.Status401Unauthorized,
                    "authentication_required", "Authentication is required.");
            }

            var submission = await _captureAccess.FindBySubmissionIdAsync(
                submissionId, cancellationToken);
            if (submission is null ||
                !Guid.TryParseExact(submission.OwnerUserId, "D", out var ownerId) ||
                !Guid.TryParseExact(userId, "D", out var actorId) ||
                ownerId != actorId)
            {
                return SubmissionNotFound();
            }

            if (submission.Status != HunterSubmissionStatus.Draft)
            {
                return Error(StatusCodes.Status409Conflict,
                    "hunter_submission_not_eligible",
                    "The submission cannot start a capture session in its current state.");
            }

            var result = await _startCapture.HandleAsync(
                userId,
                new HunterStartCaptureSessionRequestDto
                {
                    HunterSubmissionId = submissionId
                },
                cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            // Recheck after a concurrent lifecycle/ownership change. Missing and
            // foreign submissions remain indistinguishable; a now-ineligible
            // owned submission is reported as a conflict.
            try
            {
                var currentAccount = await _accounts.FindByUserIdAsync(
                    userId, cancellationToken);
                if (!TrustedIdentityAccessContract.IsActiveAccountOwner(
                        userId, currentAccount))
                {
                    return Error(StatusCodes.Status401Unauthorized,
                        "authentication_required", "Authentication is required.");
                }

                var latest = await _captureAccess.FindBySubmissionIdAsync(
                    submissionId, cancellationToken);
                if (latest is not null &&
                    Guid.TryParseExact(latest.OwnerUserId, "D", out var latestOwner) &&
                    Guid.TryParseExact(userId, "D", out var latestActor) &&
                    latestOwner == latestActor)
                {
                    return latest.Status == HunterSubmissionStatus.Draft
                        ? Error(StatusCodes.Status401Unauthorized,
                            "authentication_required", "Authentication is required.")
                        : Error(StatusCodes.Status409Conflict,
                            "hunter_submission_not_eligible",
                            "The submission cannot start a capture session in its current state.");
                }
                return SubmissionNotFound();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return CaptureUnavailable();
            }
        }
        catch (Exception)
        {
            return CaptureUnavailable();
        }
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

    private ObjectResult SubmissionNotFound() => Error(
        StatusCodes.Status404NotFound,
        "hunter_submission_not_found",
        "The requested submission was not found.");

    private ObjectResult CaptureUnavailable() => Error(
        StatusCodes.Status503ServiceUnavailable,
        "hunter_capture_unavailable",
        "A capture session could not be created.");
}
