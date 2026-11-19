
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TravelPlanning.HistoricalRoutes;

// Trusted M3 evidence only.
// Never populate this record directly from mobile JSON.
public sealed record M3VerifiedHistoricalEvidence(
    Guid TripPlanId,
    Guid SessionId,
    string AuthorizedViewerUserId,
    bool ReusePermissionGranted,
    bool CompletionVerifiedByM3,
    LiveTripStatus SessionStatus,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    IReadOnlyList<M3VerifiedStopVisit> VerifiedVisits
);

public sealed record M3VerifiedStopVisit(
    Guid TripPlanId,
    Guid SessionId,
    Guid StopId,
    int VisitSequence,
    DateTimeOffset VerifiedAtUtc
);

// Historical segment reference only.
// No private GPS trails, geometry, memories or ETA.
public sealed record HistoricalConsecutiveSegment(
    Guid SourceTripPlanId,
    Guid SourceSessionId,
    Guid FromStopId,
    Guid ToStopId,
    int FromOrderIndex,
    int ToOrderIndex,
    DateTimeOffset FromVerifiedAtUtc,
    DateTimeOffset ToVerifiedAtUtc
);
