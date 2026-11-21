using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

// Staging seam. An approved adapter must read the current authoritative route
// for this trip. Missing or superseded geometry must be returned as unavailable.
public interface IAuthoritativeTripHazardRouteReader
{
    Task<IReadOnlyList<RouteWaypoint>?> ReadAsync(Guid tripPlanId, CancellationToken cancellationToken);
}
