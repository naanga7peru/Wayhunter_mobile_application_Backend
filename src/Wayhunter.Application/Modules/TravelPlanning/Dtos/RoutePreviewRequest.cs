
using System.Collections.Generic;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

public sealed record RoutePreviewRequest(
    IReadOnlyList<RouteWaypoint> OrderedStops
);
