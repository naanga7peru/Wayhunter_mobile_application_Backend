using System;
using System.Collections.Generic;
using System.Text;


namespace Wayhunter.Domain.Modules.TravelPlanning.Entities;

public sealed class TripStop
{
    public Guid Id { get; private set; }
    public Guid TripPlanId { get; private set; }

    public int OrderIndex { get; private set; }

    public string Name { get; private set; }
    public string? PlaceId { get; private set; }

    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    public TripStop(
        Guid tripPlanId,
        int orderIndex,
        string name,
        string? placeId = null,
        double? latitude = null,
        double? longitude = null)
    {
        if (tripPlanId == Guid.Empty)
            throw new ArgumentException("Invalid Trip Plan ID.");

        if (orderIndex < 0)
            throw new ArgumentException("Invalid stop order.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stop name is required.");

        if (latitude.HasValue != longitude.HasValue)
            throw new ArgumentException(
                "Latitude and longitude must be provided together.");

        if (latitude.HasValue &&
            (latitude.Value < -90 || latitude.Value > 90))
            throw new ArgumentException("Invalid latitude.");

        if (longitude.HasValue &&
            (longitude.Value < -180 || longitude.Value > 180))
            throw new ArgumentException("Invalid longitude.");

        Id = Guid.NewGuid();
        TripPlanId = tripPlanId;
        OrderIndex = orderIndex;
        Name = name.Trim();
        PlaceId = placeId;
        Latitude = latitude;
        Longitude = longitude;
    }

    internal void SetOrder(int orderIndex)
    {
        if (orderIndex < 0)
            throw new ArgumentException("Invalid stop order.");

        OrderIndex = orderIndex;
    }
}

