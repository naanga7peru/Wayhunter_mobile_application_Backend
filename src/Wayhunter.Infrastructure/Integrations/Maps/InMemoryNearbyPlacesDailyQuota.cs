
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Wayhunter.Infrastructure.Integrations.Maps;

// Single-server demo limit.
// Production requires shared durable storage.
public sealed class InMemoryNearbyPlacesDailyQuota
{
    private sealed class Counter
    {
        public int Used;
    }

    private readonly ConcurrentDictionary<
        (Guid UserId, DateOnly Day), Counter> _counters = new();

    private readonly TimeProvider _clock;

    public InMemoryNearbyPlacesDailyQuota(TimeProvider clock)
    {
        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
    }

    public bool TryAcquire(
        string authenticatedUserId,
        int dailyLimit = 20)
    {
        if (!Guid.TryParseExact(
                authenticatedUserId, "D", out var userId) ||
            userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Trusted user ID required.");
        }

        if (dailyLimit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(dailyLimit));

        var day = DateOnly.FromDateTime(
            _clock.GetUtcNow().UtcDateTime);

        var counter = _counters.GetOrAdd(
            (userId, day), _ => new Counter());

        while (true)
        {
            var current = Volatile.Read(ref counter.Used);

            if (current >= dailyLimit)
                return false;

            if (Interlocked.CompareExchange(
                    ref counter.Used,
                    current + 1,
                    current) == current)
            {
                return true;
            }
        }
    }
}
