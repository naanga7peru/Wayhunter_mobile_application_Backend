
using System;
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

    // Existing update contract retained for compatibility.
    Task UpdateAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default);

    // Concurrency-aware update contract.
    //
    // SQL Server EF repository MUST override this method
    // and configure the client's token as the OriginalValue
    // of the EF Core RowVersion property before SaveChangesAsync.
    //
    // The default implementation is a compatibility fallback
    // for existing non-SQL repositories and test fakes.
    // It is NOT atomic against concurrent writes.
    async Task UpdateWithConcurrencyAsync(
        TripPlan tripPlan,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tripPlan);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        if (expectedRowVersion.Length != 8)
        {
            throw new ArgumentException(
                "A valid 8-byte RowVersion is required.",
                nameof(expectedRowVersion));
        }

        var current = await GetByIdAsync(
            tripPlan.Id,
            cancellationToken);

        if (current is null)
        {
            throw new KeyNotFoundException(
                "Trip plan not found.");
        }

        if (current.RowVersion is null ||
            !current.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new TripPlanConcurrencyException(tripPlan.Id);
        }

        await UpdateAsync(
            tripPlan,
            cancellationToken);
    }
}
