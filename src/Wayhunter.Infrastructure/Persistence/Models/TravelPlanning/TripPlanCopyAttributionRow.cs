using System;

namespace Wayhunter.Infrastructure.Persistence.Models.TravelPlanning;

// Source owner reference is retained for provenance and never exposed publicly.
public sealed class TripPlanCopyAttributionRow
{
    public Guid TripPlanId { get; set; }
    public Guid SourceTripPlanId { get; set; }
    public string SourceOwnerUserId { get; set; } = string.Empty;
    public DateTimeOffset CopiedAtUtc { get; set; }
}
