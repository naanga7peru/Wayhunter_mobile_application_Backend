using System;
using System.Collections.Generic;
using System.Text;


using Xunit;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TripPlanTests
{
    private static TripPlan CreateValidTrip()
    {
        return new TripPlan(
            ownerUserId: "user-001",
            tripName: "Jaffna Tour",
            startDate: new DateOnly(2026, 11, 20),
            endDate: new DateOnly(2026, 11, 22),
            budgetLkr: 15000m,
            vehicleType: "Car",
            headcount: 3
        );
    }

    [Fact]
    public void CreateTrip_ValidData_ShouldSucceed()
    {
        var trip = CreateValidTrip();

        Assert.NotEqual(Guid.Empty, trip.Id);
        Assert.Equal("Jaffna Tour", trip.TripName);
        Assert.Equal("user-001", trip.OwnerUserId);
        Assert.Equal(15000m, trip.BudgetLkr);
        Assert.Equal(3, trip.Headcount);
        Assert.Empty(trip.Stops);
    }

    [Fact]
    public void CreateTrip_InvalidDates_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TripPlan(
                "user-001",
                "Invalid Trip",
                new DateOnly(2026, 11, 25),
                new DateOnly(2026, 11, 20),
                10000m,
                "Bus",
                2
            ));
    }

    [Fact]
    public void CreateTrip_NegativeBudget_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TripPlan(
                "user-001",
                "Budget Test",
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 11, 21),
                -500m,
                "Car",
                2
            ));
    }

    [Fact]
    public void AddStop_ShouldMaintainCorrectOrder()
    {
        var trip = CreateValidTrip();

        var first = trip.AddStop("Jaffna Fort");
        var second = trip.AddStop("Nallur Temple");

        Assert.Equal(2, trip.Stops.Count);
        Assert.Equal(0, first.OrderIndex);
        Assert.Equal(1, second.OrderIndex);
        Assert.Equal(trip.Id, first.TripPlanId);
    }

    [Fact]
    public void RemoveStop_ShouldReorderRemainingStops()
    {
        var trip = CreateValidTrip();

        trip.AddStop("Jaffna Fort");
        var second = trip.AddStop("Nallur Temple");
        trip.AddStop("Point Pedro");

        bool removed = trip.RemoveStop(second.Id);

        Assert.True(removed);
        Assert.Equal(2, trip.Stops.Count);
        Assert.Equal(0, trip.Stops[0].OrderIndex);
        Assert.Equal(1, trip.Stops[1].OrderIndex);
        Assert.Equal("Point Pedro", trip.Stops[1].Name);
    }

    [Fact]
    public void AddStop_InvalidCoordinates_ShouldThrowException()
    {
        var trip = CreateValidTrip();

        Assert.Throws<ArgumentException>(() =>
            trip.AddStop(
                "Invalid Place",
                latitude: 100,
                longitude: 80
            ));
    }
}

