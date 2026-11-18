
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface IHazardReportRepository
{
    Task AddAsync(
        HazardReport hazard,
        CancellationToken cancellationToken = default);

    Task<HazardReport?> GetByIdAsync(
        Guid hazardId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HazardReport>> GetActiveAtAsync(
        DateTimeOffset serverTimeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HazardReport>> GetDueForExpiryAsync(
        DateTimeOffset serverTimeUtc,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        HazardReport hazard,
        CancellationToken cancellationToken = default);
}
