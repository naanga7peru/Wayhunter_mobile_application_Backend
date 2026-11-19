
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class InMemoryHazardReportRepository
    : IHazardReportRepository
{
    private readonly object _sync = new();

    private readonly Dictionary<Guid, HazardReport>
        _hazards = new();

    public Task AddAsync(
        HazardReport hazard,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(hazard);

        lock (_sync)
        {
            if (!_hazards.TryAdd(hazard.Id, hazard))
            {
                throw new InvalidOperationException(
                    "Hazard already exists.");
            }
        }

        return Task.CompletedTask;
    }

    public Task<HazardReport?> GetByIdAsync(
        Guid hazardId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _hazards.TryGetValue(hazardId, out var hazard);

            return Task.FromResult(hazard);
        }
    }

    public Task<IReadOnlyList<HazardReport>> GetActiveAtAsync(
        DateTimeOffset serverTimeUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            IReadOnlyList<HazardReport> result = _hazards.Values
                .Where(x => x.IsWarningActive(serverTimeUtc))
                .OrderByDescending(x => x.ReportedAtUtc)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<HazardReport>>
        GetDueForExpiryAsync(
            DateTimeOffset serverTimeUtc,
            int maxResults,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (maxResults < 1)
        {
            throw new ArgumentException(
                "Positive batch size is required.");
        }

        lock (_sync)
        {
            IReadOnlyList<HazardReport> result = _hazards.Values
                .Where(x =>
                    (x.Status == HazardStatus.Reported ||
                     x.Status == HazardStatus.Verified) &&
                    x.ExpiresAtUtc <= serverTimeUtc)
                .OrderBy(x => x.ExpiresAtUtc)
                .Take(maxResults)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task UpdateAsync(
        HazardReport hazard,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(hazard);

        lock (_sync)
        {
            if (!_hazards.ContainsKey(hazard.Id))
            {
                throw new KeyNotFoundException(
                    "Hazard report was not found.");
            }

            _hazards[hazard.Id] = hazard;
        }

        return Task.CompletedTask;
    }
}
