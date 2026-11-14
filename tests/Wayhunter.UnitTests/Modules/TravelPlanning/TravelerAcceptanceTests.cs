
using System;
using System.Collections.Generic;
using Xunit;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelerAcceptanceTests
{
    private static IReadOnlyList<string> Validate(
        string tripName = "Jaffna Tour",
        string vehicleType = "Car",
        int headcount = 2,
        decimal budget = 5000m,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        return TripPlanValidator.ValidateCreate(
            tripName,
            startDate ?? new DateOnly(2026, 11, 20),
            endDate ?? new DateOnly(2026, 11, 22),
            budget,
            vehicleType,
            headcount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTripName_ShouldBeRejected(string name)
    {
        var errors = Validate(tripName: name);

        Assert.Contains(
            "Trip name is required.", errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyVehicle_ShouldBeRejected(string vehicle)
    {
        var errors = Validate(vehicleType: vehicle);

        Assert.Contains(
            "Vehicle type is required.", errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidHeadcount_ShouldBeRejected(int count)
    {
        var errors = Validate(headcount: count);

        Assert.Contains(
            "Headcount must be at least 1.", errors);
    }

    [Fact]
    public void NegativeBudget_ShouldBeRejected()
    {
        var errors = Validate(budget: -100m);

        Assert.Contains(
            "Budget cannot be negative.", errors);
    }

    [Fact]
    public void SameDayTripWithZeroBudget_ShouldBeValid()
    {
        var date = new DateOnly(2026, 11, 20);

        var errors = Validate(
            budget: 0m,
            startDate: date,
            endDate: date);

        Assert.Empty(errors);
    }

    [Fact]
    public void EndDateBeforeStartDate_ShouldBeRejected()
    {
        var errors = Validate(
            startDate: new DateOnly(2026, 11, 25),
            endDate: new DateOnly(2026, 11, 20));

        Assert.Contains(
            "End date cannot be before start date.",
            errors);
    }

    [Fact]
    public void InvalidUpdate_ShouldPreserveOriginalTrip()
    {
        var trip = CreateTrip();

        Assert.Throws<ArgumentException>(() =>
            trip.UpdateDetails(
                "Changed Trip",
                new DateOnly(2026, 11, 21),
                new DateOnly(2026, 11, 24),
                20000m,
                "Bus",
                0));

        Assert.Equal("Jaffna Tour", trip.TripName);
        Assert.Equal(5000m, trip.BudgetLkr);
        Assert.Equal("Car", trip.VehicleType);
        Assert.Equal(2, trip.Headcount);
        Assert.Equal(TripDraftStatus.Draft,
            trip.DraftStatus);
    }

    [Fact]
    public void InvalidVehicleUpdate_ShouldPreserveTrip()
    {
        var trip = CreateTrip();

        Assert.Throws<ArgumentException>(() =>
            trip.UpdateDetails(
                "Changed Trip",
                new DateOnly(2026, 11, 21),
                new DateOnly(2026, 11, 24),
                20000m,
                " ",
                3));

        Assert.Equal("Jaffna Tour", trip.TripName);
        Assert.Equal("Car", trip.VehicleType);
        Assert.Equal(5000m, trip.BudgetLkr);
    }

    [Fact]
    public void MultipleInvalidFields_ShouldReturnAllErrors()
    {
        var errors = Validate(
            tripName: "",
            vehicleType: "",
            headcount: 0,
            budget: -1m,
            startDate: new DateOnly(2026, 11, 25),
            endDate: new DateOnly(2026, 11, 20));

        Assert.Equal(5, errors.Count);
    }

    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "user-001",
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            5000m,
            "Car",
            2);
    }
}
