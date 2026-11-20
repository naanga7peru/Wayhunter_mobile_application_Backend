using System;
using System.Collections.Generic;
using System.Linq;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

internal sealed class BoundedWeatherCache<T>(int capacity) where T : class
{
    private readonly object _gate = new();
    private readonly Dictionary<(double, double), (T Value, DateTimeOffset CachedAtUtc)> _entries = new();

    public (T Value, DateTimeOffset CachedAtUtc)? Read((double, double) key)
    {
        lock (_gate)
            return _entries.TryGetValue(key, out var entry) ? entry : null;
    }

    public void Write((double, double) key, T value, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing) && existing.CachedAtUtc > now)
                return;

            if (_entries.Count >= capacity && !_entries.ContainsKey(key))
                _entries.Remove(_entries.MinBy(entry => entry.Value.CachedAtUtc).Key);

            _entries[key] = (value, now);
        }
    }
}
