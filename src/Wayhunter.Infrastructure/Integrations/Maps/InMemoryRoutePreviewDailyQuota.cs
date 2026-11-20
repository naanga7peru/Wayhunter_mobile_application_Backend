using System;

namespace Wayhunter.Infrastructure.Integrations.Maps;

public sealed class InMemoryRoutePreviewDailyQuota(TimeProvider clock)
{
    private readonly InMemoryTravelProviderQuota _quota = new(clock);

    public bool TryAcquire(string authenticatedUserId, int dailyLimit = 20) =>
        _quota.TryAcquire(authenticatedUserId, dailyLimit);
}
