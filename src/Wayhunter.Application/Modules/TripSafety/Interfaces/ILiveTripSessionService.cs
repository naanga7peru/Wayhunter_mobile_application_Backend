
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ILiveTripSessionService
{
    Task<LiveTripSession> StartAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default);

    Task<LiveTripSession> PauseAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<LiveTripSession> ResumeAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<LiveTripSession> FinishAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
