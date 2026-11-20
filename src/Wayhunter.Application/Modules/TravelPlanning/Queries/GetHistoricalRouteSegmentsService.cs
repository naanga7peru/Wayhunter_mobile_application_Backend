
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.HistoricalRoutes;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

public sealed class GetHistoricalRouteSegmentsService
{
    private readonly ITripPlanRepository _tripPlans;
    private readonly ITrustedHistoricalEvidenceReader _evidenceReader;

    public GetHistoricalRouteSegmentsService(
        ITripPlanRepository tripPlans,
        ITrustedHistoricalEvidenceReader evidenceReader)
    {
        _tripPlans = tripPlans
            ?? throw new ArgumentNullException(nameof(tripPlans));

        _evidenceReader = evidenceReader
            ?? throw new ArgumentNullException(nameof(evidenceReader));
    }

    public async Task<IReadOnlyList<HistoricalConsecutiveSegment>>
        GetAsync(
            Guid tripPlanId,
            string authenticatedViewerUserId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (string.IsNullOrWhiteSpace(authenticatedViewerUserId))
        {
            throw new ArgumentException(
                "Trusted authenticated user is required.",
                nameof(authenticatedViewerUserId));
        }

        // M2 reads the original planned trip.
        var trip = await _tripPlans.GetByIdAsync(
            tripPlanId,
            cancellationToken);

        if (trip is null)
        {
            return Array.Empty<HistoricalConsecutiveSegment>();
        }

        // M3 must verify session completion,
        // viewer authorization and reuse permission.
        var evidence = await _evidenceReader.GetForViewerAsync(
            tripPlanId,
            authenticatedViewerUserId,
            cancellationToken);

        // Never fabricate a completed trip or stop visit.
        if (evidence is null)
        {
            return Array.Empty<HistoricalConsecutiveSegment>();
        }

        // Reuse existing M2-MS06-T01 validation.
        return HistoricalConsecutiveSegmentValidator.Validate(
            trip,
            authenticatedViewerUserId,
            evidence);
    }
}
