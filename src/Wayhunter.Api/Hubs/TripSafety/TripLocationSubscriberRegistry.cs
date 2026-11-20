
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

    private readonly Dictionary<string, TripLocationSubscriber>
        _connections = new(StringComparer.Ordinal);

    public void Register(
        string connectionId,
        Guid tripPlanId,
        Guid sessionId,
        string userId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException(
                "Connection ID is required.",
                nameof(connectionId));

        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
            throw new ArgumentException(
                "Valid trip and session IDs are required.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException(
                "Authenticated user is required.");

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
                connectionId,
                out var subscriber)
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

    public void Remove(string connectionId)
    {
        lock (_sync)
        {
            _connections.Remove(connectionId);
        }
    }

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

    public IReadOnlyList<TripLocationSubscriber> RevokeSession(
        Guid tripPlanId,
        Guid sessionId)
    {
        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
            throw new ArgumentException(
                "Valid trip and session IDs are required.");

        return RemoveMatching(x =>
            x.TripPlanId == tripPlanId &&
            x.SessionId == sessionId);
    }

    public IReadOnlyList<TripLocationSubscriber> RevokeTripMember(
        Guid tripPlanId,
        string userId)
    {
        if (tripPlanId == Guid.Empty)
            throw new ArgumentException(
                "Valid trip ID is required.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException(
                "User ID is required.");

        var normalized = userId.Trim();

        return RemoveMatching(x =>
            x.TripPlanId == tripPlanId &&
            string.Equals(
                x.UserId,
                normalized,
                StringComparison.Ordinal));
    }

    public IReadOnlyList<TripLocationSubscriber>
        RevokeMemberFromSession(
            Guid tripPlanId,
            Guid sessionId,
            string userId)
    {
        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
            throw new ArgumentException(
                "Valid trip and session IDs are required.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException(
                "User ID is required.");

        var normalized = userId.Trim();

        return RemoveMatching(x =>
            x.TripPlanId == tripPlanId &&
            x.SessionId == sessionId &&
            string.Equals(
                x.UserId,
                normalized,
                StringComparison.Ordinal));
    }

    private IReadOnlyList<TripLocationSubscriber> RemoveMatching(
        Func<TripLocationSubscriber, bool> predicate)
    {
        lock (_sync)
        {
            var removed = _connections.Values
                .Where(predicate)
                .ToArray();

            foreach (var subscriber in removed)
            {
                _connections.Remove(
                    subscriber.ConnectionId);
            }

            return removed;
        }
    }
}
