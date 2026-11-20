using System;
using Wayhunter.Infrastructure.Integrations.Maps;

namespace Wayhunter.Infrastructure.Integrations.Ai;

public sealed class InMemoryAiTripDailyQuota(TimeProvider clock)
{
    private readonly InMemoryTravelProviderQuota _quota = new(clock);

    public bool TryAcquire(string authenticatedUserId, int maxRequestsPerDay) =>
        _quota.TryAcquire(authenticatedUserId, maxRequestsPerDay);
}
