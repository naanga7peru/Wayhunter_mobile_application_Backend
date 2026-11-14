
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class StartTripEligibilityValidatorTests
{
    private static TripPlan CreateTripPlan()
    {
        return new TripPlan(
            "user-001",
            "Jaffna Adventure",
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 17),
            15000m,
            "Car",
            4);
    }

    [Fact]
    public void Valid_Owner_Should_Be_Allowed()
    {
        var plan = CreateTripPlan();

        var exception = Record.Exception(() =>
            StartTripEligibilityValidator.EnsureOwnerMayStart(
                plan,
                "user-001"));

        Assert.Null(exception);
    }

    [Fact]
    public void UserId_With_Spaces_Should_Be_Allowed()
    {
        var plan = CreateTripPlan();

        var exception = Record.Exception(() =>
            StartTripEligibilityValidator.EnsureOwnerMayStart(
                plan,
                "  user-001  "));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_Authentication_Should_Fail(
        string? userId)
    {
        var plan = CreateTripPlan();

        Assert.Throws<UnauthorizedAccessException>(() =>
            StartTripEligibilityValidator.EnsureOwnerMayStart(
                plan,
                userId));
    }

    [Fact]
    public void Missing_TripPlan_Should_Fail()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            StartTripEligibilityValidator.EnsureOwnerMayStart(
                null,
                "user-001"));
    }

    [Fact]
    public void Another_User_Cannot_Start_Trip()
    {
        var plan = CreateTripPlan();

        Assert.Throws<UnauthorizedAccessException>(() =>
            StartTripEligibilityValidator.EnsureOwnerMayStart(
                plan,
                "user-002"));
    }

    [Fact]
    public void Cancelled_Trip_Cannot_Be_Started()
    {
        var plan = CreateTripPlan();

        plan.Cancel();

        Assert.Equal(
            TripDraftStatus.Cancelled,
            plan.DraftStatus);

        Assert.Throws<InvalidOperationException>(() =>
            StartTripEligibilityValidator.EnsureOwnerMayStart(
                plan,
                "user-001"));
    }

    [Fact]
    public void UserId_Comparison_Should_Be_Case_Sensitive()
    {
        var plan = CreateTripPlan();

        Assert.Throws<UnauthorizedAccessException>(() =>
            StartTripEligibilityValidator.EnsureOwnerMayStart(
                plan,
                "USER-001"));
    }

    [Fact]
    public void Validation_Should_Not_Change_Trip_Status()
    {
        var plan = CreateTripPlan();

        StartTripEligibilityValidator.EnsureOwnerMayStart(
            plan,
            "user-001");

        Assert.Equal(
            TripDraftStatus.Draft,
            plan.DraftStatus);
    }
}
