using System;
using System.Collections.Generic;
using System.Linq;

namespace Wayhunter.Api.Hubs.TripSafety;

public sealed record TripLocationSubscriber(
    string ConnectionId,
    Guid TripPlanId,
    Guid SessionId,
    string UserId);

public sealed class TripLocationSubscriberRegistry
{
    private readonly object _sync = new();

    private readonly Dictionary<
        string, TripLocationSubscriber>
        _connections =
            new(StringComparer.Ordinal);

    public void Register(
        string connectionId,
        Guid tripPlanId,
        Guid sessionId,
        string userId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            throw new ArgumentException(
                "Connection ID is required.",
                nameof(connectionId));
        }

        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid trip and session IDs are required.");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user is required.");
        }

        var subscriber = new TripLocationSubscriber(
            connectionId,
            tripPlanId,
            sessionId,
            userId.Trim());

        lock (_sync)
        {
            _connections[connectionId] = subscriber;
        }
    }

    public TripLocationSubscriber? Find(
        string connectionId)
    {
        lock (_sync)
        {
            return _connections.TryGetValue(
                connectionId, out var subscriber)
                    ? subscriber
                    : null;
        }
    }

    public IReadOnlyList<TripLocationSubscriber> Snapshot(
        Guid tripPlanId,
        Guid sessionId)
    {
        lock (_sync)
        {
            return _connections.Values
                .Where(x =>
                    x.TripPlanId == tripPlanId &&
                    x.SessionId == sessionId)
                .ToArray();
        }
    }

    public void Remove(
        string connectionId)
    {
        lock (_sync)
        {
            _connections.Remove(connectionId);
        }
    }

    // Remove an entry only if it is still the
    // same subscription observed earlier.
    public void RemoveIfUnchanged(
        TripLocationSubscriber subscriber)
    {
        lock (_sync)
        {
            if (_connections.TryGetValue(
                    subscriber.ConnectionId,
                    out var current) &&
                current == subscriber)
            {
                _connections.Remove(
                    subscriber.ConnectionId);
            }
        }
    }
}
