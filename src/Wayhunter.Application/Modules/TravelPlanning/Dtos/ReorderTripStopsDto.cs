
using System;
using System.Collections.Generic;

namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

public sealed record ReorderTripStopsDto(
    IReadOnlyList<Guid> StopIds,
    byte[]? ExpectedRowVersion = null
);
