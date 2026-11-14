
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed class LiveTripSessionService : ILiveTripSessionService
{
    private readonly ILiveTripSessionRepository _repository;
    private readonly TimeProvider _timeProvider;

    public LiveTripSessionService(
        ILiveTripSessionRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<LiveTripSession> StartAsync(
        string tripPlanId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tripPlanId))
        {
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        string normalizedTripPlanId = tripPlanId.Trim();

        var existing = await _repository.GetOpenByTripPlanIdAsync(
            normalizedTripPlanId,
            cancellationToken);

        // NEW: Repeated Start returns the existing session.
        // Do not create another Active or Paused session.
        if (existing != null)
        {
            return existing;
        }

        var session = LiveTripSession.Start(
            Guid.NewGuid(),
            normalizedTripPlanId,
            _timeProvider.GetUtcNow());

        await _repository.AddAsync(
            session,
            cancellationToken);

        return session;
    }

    public async Task<LiveTripSession> PauseAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetRequiredSessionAsync(
            sessionId,
            cancellationToken);

        session.Pause();

        await _repository.UpdateAsync(
            session,
            cancellationToken);

        return session;
    }

    public async Task<LiveTripSession> ResumeAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetRequiredSessionAsync(
            sessionId,
            cancellationToken);

        session.Resume();

        await _repository.UpdateAsync(
            session,
            cancellationToken);

        return session;
    }

    public async Task<LiveTripSession> FinishAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetRequiredSessionAsync(
            sessionId,
            cancellationToken);

        if (session.Status == LiveTripStatus.Finished)
        {
            return session;
        }

        session.Finish(_timeProvider.GetUtcNow());

        await _repository.UpdateAsync(
            session,
            cancellationToken);

        return session;
    }

    private async Task<LiveTripSession> GetRequiredSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Session ID is required.",
                nameof(sessionId));
        }

        var session = await _repository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session == null)
        {
            throw new KeyNotFoundException(
                "Live trip session was not found.");
        }

        return session;
    }
}
