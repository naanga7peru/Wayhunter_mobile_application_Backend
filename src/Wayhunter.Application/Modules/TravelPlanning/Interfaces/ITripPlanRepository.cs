
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

public interface ITripPlanRepository
{
    Task<TripPlan?> GetByIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default);

    async Task UpdateWithConcurrencyAsync(
        TripPlan tripPlan,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tripPlan);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        if (expectedRowVersion.Length != 8)
            throw new ArgumentException(
                "A valid 8-byte RowVersion is required.");

        var current = await GetByIdAsync(
            tripPlan.Id, cancellationToken);

        if (current is null)
            throw new KeyNotFoundException(
                "Trip plan not found.");

        if (current.RowVersion is null ||
            !current.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new TripPlanConcurrencyException(tripPlan.Id);
        }

        await UpdateAsync(tripPlan, cancellationToken);
    }

    // Default fallback for InMemory repositories and test fakes.
    // SQL repository MUST override this for atomic removal.
    async Task<TripPlan> RemoveStopWithConcurrencyAsync(
        TripPlan tripPlan,
        Guid stopId,
        byte[]? expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tripPlan);

        if (tripPlan.RowVersion.Length == 8)
        {
            if (expectedRowVersion is null ||
                expectedRowVersion.Length != 8)
            {
                throw new ArgumentException(
                    "A valid RowVersion is required.");
            }

            if (!tripPlan.RowVersion.SequenceEqual(
                    expectedRowVersion))
            {
                throw new TripPlanConcurrencyException(tripPlan.Id);
            }
        }

        if (!tripPlan.RemoveStop(stopId))
            throw new KeyNotFoundException(
                "Trip stop not found.");

        if (tripPlan.RowVersion.Length == 8)
        {
            await UpdateWithConcurrencyAsync(
                tripPlan,
                expectedRowVersion!,
                cancellationToken);
        }
        else
        {
            await UpdateAsync(tripPlan, cancellationToken);
        }

        return tripPlan;
    }
}
