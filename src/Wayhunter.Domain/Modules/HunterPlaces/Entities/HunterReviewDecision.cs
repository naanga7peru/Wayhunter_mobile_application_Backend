
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Domain.Modules.HunterPlaces.Entities;

public sealed class HunterReviewDecision
{
    // Unique review record
    public Guid Id { get; set; } = Guid.NewGuid();

    // Related Hunter submission
    public Guid HunterSubmissionId { get; set; }

    // Admin who reviewed the submission
    public string ReviewerUserId { get; set; }
        = string.Empty;

    // Approved, Rejected or Held
    public HunterSubmissionStatus ResultingStatus { get; set; }

    // Admin review reason or notes
    public string? Reason { get; set; }

    // Must be assigned by the backend server
    public DateTimeOffset ReviewedAtUtc { get; set; }
}
