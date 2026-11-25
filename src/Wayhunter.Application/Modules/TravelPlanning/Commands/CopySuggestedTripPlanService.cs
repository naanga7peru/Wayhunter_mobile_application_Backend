using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class CopySuggestedTripPlanService
{
    private readonly ICopySuggestedTripPlanStore _store;

    public CopySuggestedTripPlanService(ICopySuggestedTripPlanStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<bool> SetReuseConsentAsync(Guid tripPlanId, string ownerUserId,
        bool allowed, CancellationToken cancellationToken) =>
        _store.SetReuseConsentAsync(tripPlanId, ownerUserId, allowed, cancellationToken);

    public async Task<CopiedSuggestedTripPlanDto?> CopyAsync(
        Guid sourceTripPlanId,
        string authenticatedUserId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceTripPlanId == Guid.Empty)
            throw new ArgumentException("A source trip plan ID is required.", nameof(sourceTripPlanId));
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
            throw new ArgumentException("Authenticated user ID is required.", nameof(authenticatedUserId));

        if (string.IsNullOrEmpty(idempotencyKey) || idempotencyKey.Length > 128 ||
            idempotencyKey.Any(character => !char.IsAsciiLetterOrDigit(character) &&
                character is not '-' and not '_' and not '.'))
            throw new ArgumentException("A valid Idempotency-Key is required.", nameof(idempotencyKey));

        // The source cannot be fetched through GetByIdAsync, which is owner-only.
        // The store performs authorization + copy as one database transaction.
        var copied = await _store.CopyEligibleAsync(
            sourceTripPlanId, authenticatedUserId, idempotencyKey, cancellationToken);
        if (copied is null) return null;

        // Defense in depth: refuse misconfigured/fake stores returning wrong owner.
        if (copied.Id == sourceTripPlanId ||
            copied.OwnerUserId != authenticatedUserId ||
            copied.SourceTripPlanId != sourceTripPlanId ||
            string.IsNullOrWhiteSpace(copied.SourceOwnerUserId))
            throw new InvalidOperationException("Copied trip store returned invalid provenance.");

        var stops = copied.Stops
            .OrderBy(stop => stop.OrderIndex)
            .Select(stop => new TripStopResponseDto(
                stop.Id, stop.OrderIndex, stop.Name,
                stop.PlaceId, stop.Latitude, stop.Longitude))
            .ToArray();

        return new CopiedSuggestedTripPlanDto(
            copied.Id, sourceTripPlanId, copied.TripName,
            copied.StartDate, copied.EndDate, copied.BudgetLkr,
            copied.VehicleType, copied.Headcount, stops)
        {
            RowVersion = copied.RowVersion.ToArray()
        };
    }
}
