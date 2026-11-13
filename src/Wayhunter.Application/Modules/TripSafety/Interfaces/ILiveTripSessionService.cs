
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ILiveTripSessionService
{
    // Start a new live trip session.
    Task<LiveTripSession> StartAsync(
        string tripPlanId,
        CancellationToken cancellationToken = default);

    // Pause an active trip.
    Task<LiveTripSession> PauseAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    // Resume a paused trip.
    Task<LiveTripSession> ResumeAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    // Finish an active or paused trip.
    Task<LiveTripSession> FinishAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
