
using System;
using Xunit;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TripPlanCancelTests
{
    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "user-001",
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3
        );
    }

    [Fact]
    public void NewTrip_ShouldHaveDraftStatus()
    {
        var trip = CreateTrip();

        Assert.Equal(
            TripDraftStatus.Draft,
            trip.DraftStatus);
    }

    [Fact]
    public void Cancel_FirstTime_ShouldSucceed()
    {
        var trip = CreateTrip();

        bool result = trip.Cancel();

        Assert.True(result);
        Assert.Equal(
            TripDraftStatus.Cancelled,
            trip.DraftStatus);
    }

    [Fact]
    public void Cancel_SecondTime_ShouldReturnFalse()
    {
        var trip = CreateTrip();

        trip.Cancel();
        bool result = trip.Cancel();

        Assert.False(result);
        Assert.Equal(
            TripDraftStatus.Cancelled,
            trip.DraftStatus);
    }

    [Fact]
    public void CancelledTrip_ShouldNotAllowUpdate()
    {
        var trip = CreateTrip();
        trip.Cancel();

        Assert.Throws<InvalidOperationException>(() =>
            trip.UpdateDetails(
                "Updated Trip",
                new DateOnly(2026, 11, 21),
                new DateOnly(2026, 11, 24),
                20000m,
                "Bus",
                4
            ));

        Assert.Equal("Jaffna Tour", trip.TripName);
    }

    [Fact]
    public void CancelledTrip_ShouldNotAllowAddStop()
    {
        var trip = CreateTrip();
        trip.Cancel();

        Assert.Throws<InvalidOperationException>(() =>
            trip.AddStop("Jaffna Fort"));

        Assert.Empty(trip.Stops);
    }

    [Fact]
    public void CancelledTrip_ShouldNotAllowRemoveStop()
    {
        var trip = CreateTrip();
        var stop = trip.AddStop("Jaffna Fort");

        trip.Cancel();

        Assert.Throws<InvalidOperationException>(() =>
            trip.RemoveStop(stop.Id));

        Assert.Single(trip.Stops);
    }
}
