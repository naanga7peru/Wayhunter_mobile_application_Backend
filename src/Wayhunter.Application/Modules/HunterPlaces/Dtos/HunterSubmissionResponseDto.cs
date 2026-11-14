
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

public sealed class HunterSubmissionResponseDto
{
    public Guid SubmissionId { get; set; }

    public string PlaceName { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public HunterSubmissionStatus Status { get; set; }
        = HunterSubmissionStatus.Draft;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? SubmittedAtUtc { get; set; }
}
