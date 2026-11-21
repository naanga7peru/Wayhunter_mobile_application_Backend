using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TripSafety.Memories;

public sealed class TripMemoryService(ITripPlanRepository trips, ITripMemoryStore store, TimeProvider clock)
{
    public const int CaptionLimit = 1000;

    public async Task<TripMemoryCreateResult> CreateAsync(Guid tripId, string userId, Guid operationId,
        Guid? stopId, string? caption, TripMemoryVisibility visibility, CancellationToken ct)
    {
        ValidateId(operationId);
        var cleanCaption = caption?.Trim() ?? string.Empty;
        if (cleanCaption.Length > CaptionLimit || !Enum.IsDefined(visibility))
            throw new ArgumentException("Invalid caption or visibility.");

        var trip = await LoadTripAsync(tripId, userId, ct);
        if (stopId is not null && !trip.Stops.Any(stop => stop.Id == stopId && stop.TripPlanId == tripId))
            throw new ArgumentException("Stop must belong to the requested trip.");

        var now = clock.GetUtcNow();
        var result = await store.CreateAsync(new TripMemory(Guid.NewGuid(), tripId, stopId, userId,
            operationId, cleanCaption, visibility, now, now), ct);
        var memory = result.Memory;
        if (memory.DeletedAtUtc is not null || memory.TripPlanId != tripId ||
            !string.Equals(memory.OwnerUserId, userId, StringComparison.Ordinal) ||
            memory.CreateOperationId != operationId || memory.TripStopId != stopId ||
            memory.Caption != cleanCaption || memory.Visibility != visibility)
            throw new TripMemoryConflictException("Create operation is already bound to a different or deleted memory.");

        return new(await ToViewAsync(memory, ct), result.Created);
    }

    public async Task<TripMemoryView> GetAsync(Guid tripId, Guid memoryId, string userId, CancellationToken ct) =>
        await ToViewAsync(await FindOwnedAsync(tripId, memoryId, userId, ct), ct);

    public async Task<IReadOnlyList<TripMemoryView>> ListAsync(Guid tripId, string userId, CancellationToken ct)
    {
        await LoadTripAsync(tripId, userId, ct);
        var result = new List<TripMemoryView>();
        foreach (var memory in await store.ListOwnedAsync(tripId, userId, ct))
        {
            if (IsVisibleToOwner(memory, tripId, userId))
                result.Add(await ToViewAsync(memory, ct));
        }

        return result;
    }

    public async Task DeleteAsync(Guid tripId, Guid memoryId, string userId, CancellationToken ct)
    {
        ValidateId(memoryId);
        await LoadTripAsync(tripId, userId, ct);
        if (!await store.SoftDeleteAsync(tripId, memoryId, userId, clock.GetUtcNow(), ct))
            throw new KeyNotFoundException("Memory was not found.");
    }

    internal async Task<TripMemory> FindOwnedAsync(Guid tripId, Guid memoryId, string userId, CancellationToken ct)
    {
        ValidateId(memoryId);
        await LoadTripAsync(tripId, userId, ct);
        var memory = await store.FindAsync(tripId, memoryId, ct);
        if (memory is null || memory.MemoryId != memoryId || !IsVisibleToOwner(memory, tripId, userId))
            throw new KeyNotFoundException("Memory was not found.");
        return memory;
    }

    private async Task<Wayhunter.Domain.Modules.TravelPlanning.Entities.TripPlan> LoadTripAsync(
        Guid tripId, string userId, CancellationToken ct)
    {
        ValidateId(tripId);
        if (!Guid.TryParseExact(userId, "D", out var actor) || actor == Guid.Empty || actor.ToString("D") != userId)
            throw new UnauthorizedAccessException("Trusted identity is required.");
        if (!store.IsAvailable)
            throw new TripMemoryUnavailableException();

        var trip = await trips.GetByIdAsync(tripId, ct);
        if (trip is null || trip.Id != tripId || !string.Equals(trip.OwnerUserId, userId, StringComparison.Ordinal))
            throw new KeyNotFoundException("Trip was not found.");
        return trip;
    }

    private static bool IsVisibleToOwner(TripMemory memory, Guid tripId, string userId) =>
        memory.TripPlanId == tripId && memory.DeletedAtUtc is null &&
        string.Equals(memory.OwnerUserId, userId, StringComparison.Ordinal);

    private async Task<TripMemoryView> ToViewAsync(TripMemory memory, CancellationToken ct) =>
        new(memory.MemoryId, memory.TripPlanId, memory.TripStopId, memory.Caption, memory.Visibility,
            memory.CreatedAtUtc, memory.UpdatedAtUtc,
            await store.ReadAttachedMediaAsync(memory.MemoryId, memory.OwnerUserId, ct));

    internal static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Valid operation, trip and memory IDs are required.");
    }
}
