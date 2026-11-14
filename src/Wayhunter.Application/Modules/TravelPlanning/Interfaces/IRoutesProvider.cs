
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

// Contract for external route calculation.
// Implementation will be added in a later integration phase.
public interface IRoutesProvider
{
    Task<RouteProviderResult> CalculateRouteAsync(
        IReadOnlyList<RouteWaypoint> orderedStops,
        CancellationToken cancellationToken = default);
}

// A point on the planned route.
public sealed record RouteWaypoint(
    double Latitude,
    double Longitude
);

// Result returned by the external routing provider.
public sealed record RouteProviderResult(
    bool IsSuccess,
    IReadOnlyList<RouteWaypoint> RouteGeometry,
    double? DistanceKm,
    double? EstimatedDurationMinutes,
    string? ProviderName,
    string? FailureReason
);
