using System;
using System.Collections.Generic;
using System.Text;


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

    public IReadOnlyList<TripStop> Stops => _stops.AsReadOnly();

    public TripPlan(
        string ownerUserId,
        string tripName,
        DateOnly startDate,
        DateOnly endDate,
        decimal budgetLkr,
        string vehicleType,
        int headcount)
    {
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

        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        TripName = tripName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        BudgetLkr = budgetLkr;
        VehicleType = vehicleType.Trim();
        Headcount = headcount;
    }

    public TripStop AddStop(
        string name,
        string? placeId = null,
        double? latitude = null,
        double? longitude = null)
    {
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

    public bool RemoveStop(Guid stopId)
    {
        var stop = _stops.FirstOrDefault(s => s.Id == stopId);

        if (stop is null)
            return false;

        _stops.Remove(stop);

        for (int i = 0; i < _stops.Count; i++)
            _stops[i].SetOrder(i);

        return true;
    }
}

