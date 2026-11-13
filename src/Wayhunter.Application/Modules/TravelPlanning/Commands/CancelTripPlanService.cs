
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class CancelTripPlanService
{
    private readonly ITripPlanRepository _repository;

    public CancelTripPlanService(ITripPlanRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    // true  = cancelled successfully
    // false = already cancelled
    // null  = trip not found or not owned by user
    public async Task<bool?> CancelAsync(
        Guid tripPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (tripPlanId == Guid.Empty)
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException(
                "Authenticated User ID is required.",
                nameof(ownerUserId));

        cancellationToken.ThrowIfCancellationRequested();

        // Find the existing trip
        var trip = await _repository.GetByIdAsync(
            tripPlanId,
            cancellationToken);

        // Do not allow users to cancel another user's trip
        if (trip is null || trip.OwnerUserId != ownerUserId)
            return null;

        // Domain logic handles repeated cancellations
        bool cancelled = trip.Cancel();

        if (!cancelled)
            return false;

        // Save cancelled state
        await _repository.UpdateAsync(
            trip,
            cancellationToken);

        return true;
    }
}
