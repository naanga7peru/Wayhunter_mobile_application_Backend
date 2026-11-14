
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

public sealed class HunterReviewRequestDto
{
    // Submission that the Admin wants to review
    public Guid HunterSubmissionId { get; set; }

    // Only Approved, Rejected or Held are valid decisions.
    // Backend validation must enforce this.
    public HunterSubmissionStatus ResultingStatus { get; set; }

    // Reason for rejection or holding the submission
    public string? Reason { get; set; }
}
