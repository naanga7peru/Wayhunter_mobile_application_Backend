
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Infrastructure.Integrations.Maps;

public sealed class UnavailableHazardRoutesProvider
    : IRoutesProvider
{
    public Task<RouteProviderResult> CalculateRouteAsync(
        IReadOnlyList<RouteWaypoint> orderedStops,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new RouteProviderResult(
            false,
            Array.Empty<RouteWaypoint>(),
            null,
            null,
            null,
            "Actual route geometry provider is not configured."));
    }
}
