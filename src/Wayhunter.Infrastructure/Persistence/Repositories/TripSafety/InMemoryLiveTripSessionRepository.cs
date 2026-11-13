
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class InMemoryLiveTripSessionRepository
    : ILiveTripSessionRepository
{
    private readonly object _sync = new();

    private readonly Dictionary<Guid, LiveTripSession> _sessions = new();

    public Task<LiveTripSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _sessions.TryGetValue(sessionId, out var session);

            return Task.FromResult(session);
        }
    }

    public Task<LiveTripSession?> GetOpenByTripPlanIdAsync(
        string tripPlanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(tripPlanId))
        {
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        lock (_sync)
        {
            var session = _sessions.Values.FirstOrDefault(x =>
                x.TripPlanId == tripPlanId.Trim() &&
                x.Status != LiveTripStatus.Finished);

            return Task.FromResult(session);
        }
    }

    public Task AddAsync(
        LiveTripSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        lock (_sync)
        {
            if (_sessions.ContainsKey(session.SessionId))
            {
                throw new InvalidOperationException(
                    "Session ID already exists.");
            }

            bool hasOpenSession = _sessions.Values.Any(x =>
                x.TripPlanId == session.TripPlanId &&
                x.Status != LiveTripStatus.Finished);

            if (hasOpenSession &&
                session.Status != LiveTripStatus.Finished)
            {
                throw new InvalidOperationException(
                    "This trip already has an open session.");
            }

            _sessions.Add(session.SessionId, session);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        LiveTripSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        lock (_sync)
        {
            if (!_sessions.ContainsKey(session.SessionId))
            {
                throw new KeyNotFoundException(
                    "Live trip session was not found.");
            }

            _sessions[session.SessionId] = session;
        }

        return Task.CompletedTask;
    }
}
