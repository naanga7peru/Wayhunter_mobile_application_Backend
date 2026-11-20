
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Wayhunter.Infrastructure.Integrations.Ai;

// Local demonstration quota.
// Production requires shared, durable quota storage.
public sealed class InMemoryAiTripDailyQuota
{
    private sealed class Counter
    {
        public int Used;
    }

    private readonly TimeProvider _clock;

    private readonly ConcurrentDictionary<
        (Guid UserId, DateOnly UtcDay), Counter> _usage = new();

    public InMemoryAiTripDailyQuota(TimeProvider clock)
    {
        _clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    public bool TryAcquire(
        string authenticatedUserId,
        int maxRequestsPerDay)
    {
        if (!Guid.TryParseExact(
                authenticatedUserId, "D", out var userId) ||
            userId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trusted authenticated User ID is required.",
                nameof(authenticatedUserId));
        }

        if (maxRequestsPerDay is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRequestsPerDay));
        }

        var utcDay = DateOnly.FromDateTime(
            _clock.GetUtcNow().UtcDateTime);

        var counter = _usage.GetOrAdd(
            (userId, utcDay),
            _ => new Counter());

        while (true)
        {
            int current = Volatile.Read(ref counter.Used);

            if (current >= maxRequestsPerDay)
                return false;

            // Atomic reservation prevents concurrent
            // requests from exceeding the limit.
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
