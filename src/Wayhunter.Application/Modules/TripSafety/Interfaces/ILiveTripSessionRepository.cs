
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ILiveTripSessionRepository
{
    Task<LiveTripSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<LiveTripSession?> GetOpenByTripPlanIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        LiveTripSession session,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        LiveTripSession session,
        CancellationToken cancellationToken = default);
}
