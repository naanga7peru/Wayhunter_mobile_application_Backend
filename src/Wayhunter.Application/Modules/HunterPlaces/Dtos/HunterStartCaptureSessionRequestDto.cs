
using System;

namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

/// <summary>
/// Request to start a backend-managed Hunter capture session.
/// The authenticated Hunter identity must come from the server,
/// not from the client request.
/// </summary>
public sealed class HunterStartCaptureSessionRequestDto
{
    /// <summary>
    /// Submission for which the capture session is requested.
    /// </summary>
    public Guid HunterSubmissionId { get; set; }
}
