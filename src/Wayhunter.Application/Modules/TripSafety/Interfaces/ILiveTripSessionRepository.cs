
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ILiveTripSessionRepository
{
    // Find a live trip session using its unique ID.
    Task<LiveTripSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    // Find an existing Active or Paused session
    // belonging to a Trip Plan.
    Task<LiveTripSession?> GetOpenByTripPlanIdAsync(
        string tripPlanId,
        CancellationToken cancellationToken = default);

    // Save a newly created live trip session.
    Task AddAsync(
        LiveTripSession session,
        CancellationToken cancellationToken = default);

    // Save changes after Pause, Resume or Finish.
    Task UpdateAsync(
        LiveTripSession session,
        CancellationToken cancellationToken = default);
}
