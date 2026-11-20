
using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TravelPlanning.HistoricalRoutes;

public static class HistoricalConsecutiveSegmentValidator
{
    public static IReadOnlyList<HistoricalConsecutiveSegment> Validate(
        TripPlan sourceTrip,
        string authenticatedViewerUserId,
        M3VerifiedHistoricalEvidence? evidence)
    {
        ArgumentNullException.ThrowIfNull(sourceTrip);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            authenticatedViewerUserId);

        // 1. Validate trusted M3 completion and authorization.
        if (evidence is null ||
            sourceTrip.Id == Guid.Empty ||
            sourceTrip.DraftStatus == TripDraftStatus.Cancelled ||
            evidence.TripPlanId != sourceTrip.Id ||
            evidence.SessionId == Guid.Empty ||
            evidence.AuthorizedViewerUserId != authenticatedViewerUserId ||
            !evidence.ReusePermissionGranted ||
            !evidence.CompletionVerifiedByM3 ||
            evidence.SessionStatus != LiveTripStatus.Finished ||
            evidence.StartedAtUtc == default ||
            evidence.FinishedAtUtc is not { } finishedAt ||
            finishedAt < evidence.StartedAtUtc)
        {
            return Array.Empty<HistoricalConsecutiveSegment>();
        }

        // 2. Validate planned stop order.
        var planned = sourceTrip.Stops
            .OrderBy(stop => stop.OrderIndex)
            .ToArray();

        if (planned.Length < 2 ||
            planned.Select(stop => stop.Id).Distinct().Count()
                != planned.Length ||
            planned.Where((stop, index) =>
                stop.TripPlanId != sourceTrip.Id ||
                stop.Id == Guid.Empty ||
                stop.OrderIndex != index).Any())
        {
            return Array.Empty<HistoricalConsecutiveSegment>();
        }

        // 3. Require actual verified visits, not planned stops.
        if (evidence.VerifiedVisits is null ||
            evidence.VerifiedVisits.Count < 2)
        {
            return Array.Empty<HistoricalConsecutiveSegment>();
        }

        var plannedById = planned.ToDictionary(stop => stop.Id);

        var visits = evidence.VerifiedVisits
            .OrderBy(visit => visit.VisitSequence)
            .ToArray();

        var visitedIds = new HashSet<Guid>();

        // 4. Reject invalid visit evidence.
        for (var index = 0; index < visits.Length; index++)
        {
            var visit = visits[index];

            if (visit.TripPlanId != sourceTrip.Id ||
                visit.SessionId != evidence.SessionId ||
                visit.VisitSequence != index ||
                visit.StopId == Guid.Empty ||
                !plannedById.ContainsKey(visit.StopId) ||
                !visitedIds.Add(visit.StopId) ||
                visit.VerifiedAtUtc < evidence.StartedAtUtc ||
                visit.VerifiedAtUtc > finishedAt ||
                (index > 0 &&
                 visit.VerifiedAtUtc <
                 visits[index - 1].VerifiedAtUtc))
            {
                return Array.Empty<HistoricalConsecutiveSegment>();
            }
        }

        // 5. Generate only genuinely consecutive segments.
        var segments = new List<HistoricalConsecutiveSegment>();

        for (var index = 0; index < visits.Length - 1; index++)
        {
            var fromVisit = visits[index];
            var toVisit = visits[index + 1];

            var fromStop = plannedById[fromVisit.StopId];
            var toStop = plannedById[toVisit.StopId];

            // Never create a segment across skipped stops.
            if (toStop.OrderIndex != fromStop.OrderIndex + 1)
            {
                continue;
            }

            segments.Add(new HistoricalConsecutiveSegment(
                sourceTrip.Id,
                evidence.SessionId,
                fromStop.Id,
                toStop.Id,
                fromStop.OrderIndex,
                toStop.OrderIndex,
                fromVisit.VerifiedAtUtc,
                toVisit.VerifiedAtUtc
            ));
        }

        // No database writes. Original TripPlan stays unchanged.
        return segments;
    }
}
