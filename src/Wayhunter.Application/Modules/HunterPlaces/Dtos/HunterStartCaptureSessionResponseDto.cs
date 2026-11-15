
using System;

namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

public sealed class HunterStartCaptureSessionResponseDto
{
    public Guid CaptureSessionId { get; set; }

    public Guid HunterSubmissionId { get; set; }
}
