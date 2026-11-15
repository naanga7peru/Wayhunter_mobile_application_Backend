
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Application.Modules.HunterPlaces.Validation;

namespace Wayhunter.Application.Modules.HunterPlaces.Commands;

/// <summary>
/// Coordinates ownership validation, trusted identity
/// resolution, and Hunter capture session creation.
///
/// This is not an authentication boundary.
/// The authenticated user ID must come from trusted
/// backend authentication, never from Flutter input.
///
/// Submission status eligibility and production
/// identity mapping remain pending integration.
/// </summary>
public sealed class HunterStartAuthorizedCaptureSessionHandler
{
    private readonly HunterCaptureSubmissionOwnershipService
        _ownershipService;

    private readonly HunterCaptureSessionIdentityResolutionService
        _identityService;

    private readonly HunterStartCaptureSessionHandler
        _captureSessionHandler;

    public HunterStartAuthorizedCaptureSessionHandler(
        HunterCaptureSubmissionOwnershipService ownershipService,
        HunterCaptureSessionIdentityResolutionService identityService,
        HunterStartCaptureSessionHandler captureSessionHandler)
    {
        _ownershipService = ownershipService
            ?? throw new ArgumentNullException(
                nameof(ownershipService));

        _identityService = identityService
            ?? throw new ArgumentNullException(
                nameof(identityService));

        _captureSessionHandler = captureSessionHandler
            ?? throw new ArgumentNullException(
                nameof(captureSessionHandler));
    }

    public async Task<HunterStartCaptureSessionResponseDto> HandleAsync(
        string? authenticatedUserId,
        HunterStartCaptureSessionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated Hunter identity is required.");
        }

        ArgumentNullException.ThrowIfNull(request);

        var errors =
            HunterStartCaptureSessionRequestValidator.Validate(
                request);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join("; ", errors),
                nameof(request));
        }

        // Step 1: Verify ownership before session creation.
        await _ownershipService.EnsureOwnedAsync(
            authenticatedUserId,
            request.HunterSubmissionId,
            cancellationToken);

        // Step 2: Resolve trusted capture session identity.
        var sessionHunterId =
            await _identityService.ResolveRequiredAsync(
                authenticatedUserId,
                cancellationToken);

        // Step 3: Create the capture session.
        // This must never run if authorization fails.
        return await _captureSessionHandler.HandleAsync(
            sessionHunterId,
            request,
            cancellationToken);
    }
}
