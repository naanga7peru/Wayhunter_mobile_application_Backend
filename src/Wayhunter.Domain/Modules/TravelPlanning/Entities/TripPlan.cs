
using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.Domain.Modules.TravelPlanning.Entities;

public sealed class TripPlan
{
    private readonly List<TripStop> _stops = new();

    public Guid Id { get; private set; }

    public string OwnerUserId { get; private set; }
    public string TripName { get; private set; }

    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    public decimal BudgetLkr { get; private set; }
    public string VehicleType { get; private set; }
    public int Headcount { get; private set; }

    // Immutable attribution for a cloned suggested plan. Always set by
    // server-side copy operation, not by an incoming create/edit request.
    public Guid? SourceTripPlanId { get; private set; }
    public string? SourceOwnerUserId { get; private set; }

    // Draft lifecycle belongs to M2.
    public TripDraftStatus DraftStatus { get; private set; }
        = TripDraftStatus.Draft;

    // SQL Server optimistic concurrency token.
    public byte[] RowVersion { get; private set; }
        = Array.Empty<byte>();

    public bool AllowsSuggestedPlanReuse { get; private set; }

    public void RestoreCopyAttribution(Guid sourceId, string sourceOwnerId)
    {
        if (sourceId == Guid.Empty || sourceId == Id || string.IsNullOrWhiteSpace(sourceOwnerId) ||
            sourceOwnerId == OwnerUserId || SourceTripPlanId is not null)
            throw new InvalidOperationException("Invalid copy attribution.");

        SourceTripPlanId = sourceId;
        SourceOwnerUserId = sourceOwnerId;
    }

    public IReadOnlyList<TripStop> Stops => _stops.AsReadOnly();

    // ==========================================
    // CREATE TRIP PLAN
    // ==========================================
    public TripPlan(
        string ownerUserId,
        string tripName,
        DateOnly startDate,
        DateOnly endDate,
        decimal budgetLkr,
        string vehicleType,
        int headcount)
        : this(Guid.NewGuid(), ownerUserId, tripName, startDate, endDate,
            budgetLkr, vehicleType, headcount)
    {
    }

    /// <summary>Validated explicit ID constructor for deterministic seed/import data.</summary>
    public TripPlan(
        Guid id,
        string ownerUserId,
        string tripName,
        DateOnly startDate,
        DateOnly endDate,
        decimal budgetLkr,
        string vehicleType,
        int headcount)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Trip ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("User ID is required.");

        if (string.IsNullOrWhiteSpace(tripName))
            throw new ArgumentException("Trip name is required.");

        if (endDate < startDate)
            throw new ArgumentException(
                "End date cannot be before start date.");

        if (budgetLkr < 0)
            throw new ArgumentException(
                "Budget cannot be negative.");

        if (string.IsNullOrWhiteSpace(vehicleType))
            throw new ArgumentException(
                "Vehicle type is required.");

        if (headcount < 1)
            throw new ArgumentException(
                "Headcount must be at least 1.");

        Id = id;
        OwnerUserId = ownerUserId;
        TripName = tripName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        BudgetLkr = budgetLkr;
        VehicleType = vehicleType.Trim();
        Headcount = headcount;
    }

    // M2-MS05-T03: snapshot PLANNED stops into an independent editable draft.
    // Access and genuine completion are checked at the SQL store boundary.
    public static TripPlan CreateCopyFrom(TripPlan source, string newOwnerUserId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(newOwnerUserId);
        if (source.OwnerUserId == newOwnerUserId)
            throw new InvalidOperationException("Use the regular My Plans flow for your own trip.");
        if (source.DraftStatus == TripDraftStatus.Cancelled)
            throw new InvalidOperationException("Cannot copy a cancelled source plan.");

        var ordered = source.Stops.OrderBy(stop => stop.OrderIndex).ToArray();
        if (ordered.Length == 0 ||
            ordered.Select(stop => stop.OrderIndex).Distinct().Count() != ordered.Length ||
            ordered.Where((stop, index) => stop.OrderIndex != index).Any())
            throw new InvalidOperationException("Source plan has invalid stop order.");

        var draft = new TripPlan(newOwnerUserId, source.TripName,
            source.StartDate, source.EndDate, source.BudgetLkr,
            source.VehicleType, source.Headcount)
        {
            SourceTripPlanId = source.Id,
            SourceOwnerUserId = source.OwnerUserId
        };

        foreach (var stop in ordered)
        {
            draft.AddStop(stop.Name, stop.PlaceId, stop.Latitude, stop.Longitude);
        }

        return draft;
    }

    // ==========================================
    // UPDATE TRIP DETAILS
    // ==========================================
    public void UpdateDetails(
        string tripName,
        DateOnly startDate,
        DateOnly endDate,
        decimal budgetLkr,
        string vehicleType,
        int headcount)
    {
        EnsureEditable();

        if (string.IsNullOrWhiteSpace(tripName))
            throw new ArgumentException("Trip name is required.");

        if (endDate < startDate)
            throw new ArgumentException(
                "End date cannot be before start date.");

        if (budgetLkr < 0)
            throw new ArgumentException(
                "Budget cannot be negative.");

        if (string.IsNullOrWhiteSpace(vehicleType))
            throw new ArgumentException(
                "Vehicle type is required.");

        if (headcount < 1)
            throw new ArgumentException(
                "Headcount must be at least 1.");

        TripName = tripName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        BudgetLkr = budgetLkr;
        VehicleType = vehicleType.Trim();
        Headcount = headcount;
    }

    // ==========================================
    // ADD TRIP STOP
    // ==========================================
    public TripStop AddStop(
        string name,
        string? placeId = null,
        double? latitude = null,
        double? longitude = null)
    {
        EnsureEditable();

        var stop = new TripStop(
            Id,
            _stops.Count,
            name,
            placeId,
            latitude,
            longitude);

        _stops.Add(stop);

        return stop;
    }

    /// <summary>Validated explicit stop ID for deterministic seed/import data.</summary>
    public TripStop AddStopWithId(
        Guid stopId,
        string name,
        string? placeId = null,
        double? latitude = null,
        double? longitude = null)
    {
        EnsureEditable();
        if (_stops.Any(x => x.Id == stopId))
            throw new ArgumentException("Stop ID is already used in this trip.", nameof(stopId));
        var stop = new TripStop(stopId, Id, _stops.Count, name, placeId, latitude, longitude);
        _stops.Add(stop);
        return stop;
    }

    // ==========================================
    // REMOVE TRIP STOP
    // ==========================================
    public bool RemoveStop(Guid stopId)
    {
        EnsureEditable();

        var stop = _stops.FirstOrDefault(
            s => s.Id == stopId);

        if (stop is null)
            return false;

        _stops.Remove(stop);

        // Recalculate continuous order indexes.
        for (int i = 0; i < _stops.Count; i++)
        {
            _stops[i].SetOrder(i);
        }

        return true;
    }

    // ==========================================
    // NEW: REORDER TRIP STOPS
    // ==========================================
    public bool ReorderStops(
        IReadOnlyList<Guid> orderedStopIds)
    {
        EnsureEditable();

        ArgumentNullException.ThrowIfNull(orderedStopIds);

        // Every existing stop must appear exactly once.
        if (orderedStopIds.Count != _stops.Count)
        {
            throw new ArgumentException(
                "Every existing trip stop must be included exactly once.",
                nameof(orderedStopIds));
        }

        var stopsById = _stops.ToDictionary(
            stop => stop.Id);

        var seenIds = new HashSet<Guid>();

        // Validate IDs before modifying any stop.
        foreach (var stopId in orderedStopIds)
        {
            if (stopId == Guid.Empty ||
                !seenIds.Add(stopId) ||
                !stopsById.ContainsKey(stopId))
            {
                throw new ArgumentException(
                    "Stop IDs must be unique and belong to this trip.",
                    nameof(orderedStopIds));
            }
        }

        var changed = false;

        // Recalculate order indexes.
        for (int index = 0;
             index < orderedStopIds.Count;
             index++)
        {
            var stop = stopsById[orderedStopIds[index]];

            if (stop.OrderIndex != index)
            {
                changed = true;
            }

            stop.SetOrder(index);
        }

        // Rebuild collection in requested order.
        _stops.Clear();

        foreach (var stopId in orderedStopIds)
        {
            _stops.Add(stopsById[stopId]);
        }

        return changed;
    }

    // ==========================================
    // CANCEL TRIP PLAN
    // ==========================================
    public bool Cancel()
    {
        // Repeated cancellation is idempotent.
        if (DraftStatus == TripDraftStatus.Cancelled)
            return false;

        DraftStatus = TripDraftStatus.Cancelled;

        return true;
    }

    // ==========================================
    // CHECK EDITABLE STATUS
    // ==========================================
    private void EnsureEditable()
    {
        if (DraftStatus == TripDraftStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled trip plans cannot be edited.");
        }
    }
}
