
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Application.Modules.HunterPlaces.Validation;

namespace Wayhunter.Application.Modules.HunterPlaces.Commands;

/// <summary>
/// Application handler for starting a Hunter capture session.
/// This handler is not an authentication boundary.
/// Only trusted backend code should supply the Hunter ID.
/// </summary>
public sealed class HunterStartCaptureSessionHandler
{
    private readonly HunterCaptureSessionService _sessionService;

    public HunterStartCaptureSessionHandler(
        HunterCaptureSessionService sessionService)
    {
        _sessionService = sessionService
            ?? throw new ArgumentNullException(nameof(sessionService));
    }

    public async Task<HunterStartCaptureSessionResponseDto> HandleAsync(
        Guid authenticatedHunterId,
        HunterStartCaptureSessionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (authenticatedHunterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Authenticated Hunter ID is required.",
                nameof(authenticatedHunterId));
        }

        var errors =
            HunterStartCaptureSessionRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join("; ", errors),
                nameof(request));
        }

        var captureSessionId = await _sessionService.StartAsync(
            authenticatedHunterId,
            request.HunterSubmissionId,
            cancellationToken);

        if (captureSessionId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Capture session creation returned an invalid ID.");
        }

        return new HunterStartCaptureSessionResponseDto
        {
            CaptureSessionId = captureSessionId,
            HunterSubmissionId = request.HunterSubmissionId
        };
    }
}
