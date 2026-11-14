
using System;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class AiTripItineraryValidatorTests
{
    private static AiTripItineraryRequest ValidRequest() =>
        new(
            "Jaffna",
            "Kandy",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            20000m,
            "Car",
            2,
            "Nature and history"
        );

    private static AiTripItineraryResponse ValidResponse() =>
        new(
            new[]
            {
                new AiTripItineraryDay(
                    new DateOnly(2026, 11, 20),
                    new[]
                    {
                        new AiTripItineraryStop(
                            "Jaffna Fort",
                            "place-001",
                            new TimeOnly(10, 0),
                            500m)
                    })
            },
            500m,
            true
        );

    [Fact]
    public void ValidRequest_ShouldPass()
    {
        AiTripItineraryValidator.ValidateRequest(
            ValidRequest());
    }

    [Fact]
    public void EmptyDestination_ShouldFail()
    {
        var request = ValidRequest() with
        {
            Destination = ""
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateRequest(
                request));
    }

    [Fact]
    public void EndDateBeforeStart_ShouldFail()
    {
        var request = ValidRequest() with
        {
            EndDate = new DateOnly(2026, 11, 19)
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateRequest(
                request));
    }

    [Fact]
    public void NegativeBudget_ShouldFail()
    {
        var request = ValidRequest() with
        {
            BudgetLkr = -100m
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateRequest(
                request));
    }

    [Fact]
    public void InvalidHeadcount_ShouldFail()
    {
        var request = ValidRequest() with
        {
            Headcount = 0
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateRequest(
                request));
    }

    [Fact]
    public void ValidResponse_ShouldPass()
    {
        AiTripItineraryValidator.ValidateResponse(
            ValidRequest(),
            ValidResponse());
    }

    [Fact]
    public void EmptyDays_ShouldFail()
    {
        var response = ValidResponse() with
        {
            Days = Array.Empty<AiTripItineraryDay>()
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateResponse(
                ValidRequest(),
                response));
    }

    [Fact]
    public void DayOutsideTripDates_ShouldFail()
    {
        var response = ValidResponse() with
        {
            Days = new[]
            {
                new AiTripItineraryDay(
                    new DateOnly(2026, 11, 25),
                    ValidResponse().Days[0].Stops)
            }
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateResponse(
                ValidRequest(),
                response));
    }

    [Fact]
    public void NegativeStopCost_ShouldFail()
    {
        var response = ValidResponse() with
        {
            Days = new[]
            {
                new AiTripItineraryDay(
                    new DateOnly(2026, 11, 20),
                    new[]
                    {
                        new AiTripItineraryStop(
                            "Jaffna Fort",
                            "place-001",
                            new TimeOnly(10, 0),
                            -500m)
                    })
            }
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateResponse(
                ValidRequest(),
                response));
    }

    [Fact]
    public void MissingUserReview_ShouldFail()
    {
        var response = ValidResponse() with
        {
            RequiresUserReview = false
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateResponse(
                ValidRequest(),
                response));
    }

    [Fact]
    public void DuplicateDates_ShouldFail()
    {
        var day = ValidResponse().Days[0];

        var response = ValidResponse() with
        {
            Days = new[] { day, day }
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryValidator.ValidateResponse(
                ValidRequest(),
                response));
    }
}
