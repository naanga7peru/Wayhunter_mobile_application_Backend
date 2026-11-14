
using System;
using System.Text.Json;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class AiTripItineraryJsonParserTests
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
                            new TimeOnly(10, 30),
                            500m)
                    })
            },
            500m,
            true
        );

    private static string ToJson(
        AiTripItineraryResponse response)
    {
        return JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web));
    }

    [Fact]
    public void ValidJson_ShouldParseSuccessfully()
    {
        var result =
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                ToJson(ValidResponse()));

        Assert.Single(result.Days);
        Assert.Equal(
            "Jaffna Fort",
            result.Days[0].Stops[0].PlaceName);
        Assert.Equal(
            500m,
            result.EstimatedTotalCostLkr);
        Assert.True(result.RequiresUserReview);
    }

    [Fact]
    public void EmptyJson_ShouldFail()
    {
        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                ""));
    }

    [Fact]
    public void MalformedJson_ShouldFail()
    {
        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                "{ invalid json }"));
    }

    [Fact]
    public void NullJson_ShouldFail()
    {
        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                "null"));
    }

    [Fact]
    public void MissingDays_ShouldFail()
    {
        const string json = """
        {
            "estimatedTotalCostLkr": 500,
            "requiresUserReview": true
        }
        """;

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                json));
    }

    [Fact]
    public void NegativeTotalCost_ShouldFail()
    {
        var response = ValidResponse() with
        {
            EstimatedTotalCostLkr = -100m
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                ToJson(response)));
    }

    [Fact]
    public void OutsideTripDate_ShouldFail()
    {
        var response = ValidResponse() with
        {
            Days = new[]
            {
                new AiTripItineraryDay(
                    new DateOnly(2026, 12, 5),
                    ValidResponse().Days[0].Stops)
            }
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                ToJson(response)));
    }

    [Fact]
    public void EmptyStops_ShouldFail()
    {
        var response = ValidResponse() with
        {
            Days = new[]
            {
                new AiTripItineraryDay(
                    new DateOnly(2026, 11, 20),
                    Array.Empty<AiTripItineraryStop>())
            }
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                ToJson(response)));
    }

    [Fact]
    public void MissingUserReview_ShouldFail()
    {
        var response = ValidResponse() with
        {
            RequiresUserReview = false
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                ToJson(response)));
    }

    [Fact]
    public void InvalidOriginalRequest_ShouldFail()
    {
        var request = ValidRequest() with
        {
            BudgetLkr = -500m
        };

        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                request,
                ToJson(ValidResponse())));
    }
}
