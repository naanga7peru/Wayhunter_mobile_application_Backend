
using System;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class M2TripStopInputSafetyTests
{
    private static TripPlan CreateTrip() => new(
        "user-001",
        "Jaffna Trip",
        new DateOnly(2026, 11, 20),
        new DateOnly(2026, 11, 22),
        10000m,
        "Car",
        2);

    [Theory]
    [InlineData(double.NaN, 80.02)]
    [InlineData(double.PositiveInfinity, 80.02)]
    [InlineData(double.NegativeInfinity, 80.02)]
    [InlineData(91, 80.02)]
    [InlineData(-91, 80.02)]
    [InlineData(9.66, double.NaN)]
    [InlineData(9.66, double.PositiveInfinity)]
    [InlineData(9.66, double.NegativeInfinity)]
    [InlineData(9.66, 181)]
    [InlineData(9.66, -181)]
    public void InvalidGps_ShouldRejectStop(
        double latitude,
        double longitude)
    {
        var trip = CreateTrip();

        Assert.Throws<ArgumentException>(() =>
            trip.AddStop(
                "Invalid Stop",
                latitude: latitude,
                longitude: longitude));

        Assert.Empty(trip.Stops);
    }

    [Fact]
    public void OnlyLatitude_ShouldReject()
    {
        var trip = CreateTrip();

        Assert.Throws<ArgumentException>(() =>
            trip.AddStop(
                "Invalid Stop",
                latitude: 9.66));

        Assert.Empty(trip.Stops);
    }

    [Fact]
    public void OnlyLongitude_ShouldReject()
    {
        var trip = CreateTrip();

        Assert.Throws<ArgumentException>(() =>
            trip.AddStop(
                "Invalid Stop",
                longitude: 80.02));

        Assert.Empty(trip.Stops);
    }

    [Fact]
    public void StopNameOver200_ShouldReject()
    {
        var trip = CreateTrip();

        Assert.Throws<ArgumentException>(() =>
            trip.AddStop(new string('A', 201)));

        Assert.Empty(trip.Stops);
    }

    [Fact]
    public void PlaceIdOver256_ShouldReject()
    {
        var trip = CreateTrip();

        Assert.Throws<ArgumentException>(() =>
            trip.AddStop(
                "Valid Stop",
                placeId: new string('P', 257)));

        Assert.Empty(trip.Stops);
    }

    [Fact]
    public void ValidGps_ShouldAddStop()
    {
        var trip = CreateTrip();

        var stop = trip.AddStop(
            "Jaffna Fort",
            "place-001",
            9.66,
            80.02);

        Assert.Single(trip.Stops);
        Assert.Equal(9.66, stop.Latitude);
        Assert.Equal(80.02, stop.Longitude);
    }

    [Fact]
    public void OptionalGps_ShouldBeAllowed()
    {
        var trip = CreateTrip();

        var stop = trip.AddStop("Nallur Temple");

        Assert.Null(stop.Latitude);
        Assert.Null(stop.Longitude);
        Assert.Single(trip.Stops);
    }

    [Fact]
    public void Name_ShouldBeTrimmed()
    {
        var trip = CreateTrip();

        var stop = trip.AddStop("  Jaffna Fort  ");

        Assert.Equal("Jaffna Fort", stop.Name);
    }

    [Fact]
    public void MaximumLengthName_ShouldBeAllowed()
    {
        var trip = CreateTrip();

        var stop = trip.AddStop(new string('A', 200));

        Assert.Equal(200, stop.Name.Length);
    }
}
