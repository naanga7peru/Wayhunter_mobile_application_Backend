using System;
using System.Collections.Generic;

namespace Wayhunter.Infrastructure.Integrations.Maps;

// Single-process quota. Never evict an active user's counter to make room.
public sealed class InMemoryTravelProviderQuota
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, int> _usage = new();
    private readonly TimeProvider _clock;
    private readonly int _maxUsers;
    private DateOnly _day;

    public InMemoryTravelProviderQuota(TimeProvider clock, int maxUsers = 10000)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _maxUsers = maxUsers > 0 ? maxUsers : throw new ArgumentOutOfRangeException(nameof(maxUsers));
    }

    public bool TryAcquire(string authenticatedUserId, int dailyLimit)
    {
        if (!Guid.TryParseExact(authenticatedUserId, "D", out var userId) || userId == Guid.Empty)
            throw new ArgumentException("Trusted user ID required.", nameof(authenticatedUserId));
        if (dailyLimit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(dailyLimit));

        lock (_gate)
        {
            var day = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            if (day > _day)
            {
                _usage.Clear();
                _day = day;
            }

            var known = _usage.TryGetValue(userId, out var used);
            if (used >= dailyLimit || (!known && _usage.Count >= _maxUsers))
                return false;

            _usage[userId] = used + 1;
            return true;
        }
    }
}
