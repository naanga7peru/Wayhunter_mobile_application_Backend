
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Services;

namespace Wayhunter.Application.Modules.HunterPlaces.Commands;

/// <summary>
/// Coordinates authorized Hunter capture-session consumption.
///
/// The authenticated user ID must come from trusted
/// backend authentication, never directly from a
/// Flutter request body.
///
/// This handler does not upload or persist camera evidence.
/// </summary>
public sealed class HunterConsumeAuthorizedCaptureSessionHandler
{
    private readonly HunterCaptureSubmissionOwnershipService
        _ownershipService;

    private readonly HunterCaptureSessionIdentityResolutionService
        _identityService;

    private readonly HunterCaptureSessionService
        _sessionService;

    public HunterConsumeAuthorizedCaptureSessionHandler(
        HunterCaptureSubmissionOwnershipService ownershipService,
        HunterCaptureSessionIdentityResolutionService identityService,
        HunterCaptureSessionService sessionService)
    {
        _ownershipService = ownershipService
            ?? throw new ArgumentNullException(
                nameof(ownershipService));

        _identityService = identityService
            ?? throw new ArgumentNullException(
                nameof(identityService));

        _sessionService = sessionService
            ?? throw new ArgumentNullException(
                nameof(sessionService));
    }

    public async Task<bool> HandleAsync(
        string? authenticatedUserId,
        Guid captureSessionId,
        Guid hunterSubmissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated Hunter identity is required.");
        }

        if (captureSessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Capture session ID is required.",
                nameof(captureSessionId));
        }

        if (hunterSubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission ID is required.",
                nameof(hunterSubmissionId));
        }

        // Step 1: Verify trusted submission ownership
        // and Draft-only capture eligibility.
        await _ownershipService.EnsureCanStartCaptureAsync(
            authenticatedUserId,
            hunterSubmissionId,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Step 2: Resolve the active M1 account identity
        // through the configured trusted resolver.
        var hunterId = await _identityService.ResolveRequiredAsync(
            authenticatedUserId,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Step 3: Consume the session using server time.
        //
        // The store must atomically enforce:
        // - correct Hunter and submission
        // - Draft eligibility
        // - expiry
        // - not already consumed
        // - not revoked
        //
        // false means that the capture session is unusable.
        return await _sessionService.TryConsumeAsync(
            captureSessionId,
            hunterId,
            hunterSubmissionId,
            cancellationToken);
    }
}
