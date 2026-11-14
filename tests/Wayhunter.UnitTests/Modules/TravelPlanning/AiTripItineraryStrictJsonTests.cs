
using System;
using System.Text.Json.Nodes;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class AiTripItineraryStrictJsonTests
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

    private static JsonObject ValidJson() =>
        JsonNode.Parse("""
        {
          "days": [
            {
              "date": "2026-11-20",
              "stops": [
                {
                  "placeName": "Jaffna Fort",
                  "providerPlaceId": "place-001",
                  "arrivalTime": "10:30:00",
                  "estimatedCostLkr": 500
                }
              ]
            }
          ],
          "estimatedTotalCostLkr": 500,
          "requiresUserReview": true
        }
        """)!.AsObject();

    private static void ShouldReject(JsonObject json)
    {
        Assert.Throws<ArgumentException>(() =>
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                json.ToJsonString()));
    }

    [Fact]
    public void ValidStructure_ShouldPass()
    {
        var result =
            AiTripItineraryJsonParser.ParseAndValidate(
                ValidRequest(),
                ValidJson().ToJsonString());

        Assert.Single(result.Days);
        Assert.True(result.RequiresUserReview);
    }

    [Fact]
    public void MissingTotalCost_ShouldFail()
    {
        var json = ValidJson();
        json.Remove("estimatedTotalCostLkr");

        ShouldReject(json);
    }

    [Fact]
    public void MissingReviewFlag_ShouldFail()
    {
        var json = ValidJson();
        json.Remove("requiresUserReview");

        ShouldReject(json);
    }

    [Fact]
    public void DaysAsString_ShouldFail()
    {
        var json = ValidJson();
        json["days"] = "invalid";

        ShouldReject(json);
    }

    [Fact]
    public void MissingDayDate_ShouldFail()
    {
        var json = ValidJson();
        var day = json["days"]!.AsArray()[0]!.AsObject();

        day.Remove("date");

        ShouldReject(json);
    }

    [Fact]
    public void MissingStops_ShouldFail()
    {
        var json = ValidJson();
        var day = json["days"]!.AsArray()[0]!.AsObject();

        day.Remove("stops");

        ShouldReject(json);
    }

    [Fact]
    public void MissingPlaceName_ShouldFail()
    {
        var json = ValidJson();

        var stop = json["days"]!.AsArray()[0]!
            ["stops"]!.AsArray()[0]!.AsObject();

        stop.Remove("placeName");

        ShouldReject(json);
    }

    [Fact]
    public void MissingStopCost_ShouldFail()
    {
        var json = ValidJson();

        var stop = json["days"]!.AsArray()[0]!
            ["stops"]!.AsArray()[0]!.AsObject();

        stop.Remove("estimatedCostLkr");

        ShouldReject(json);
    }

    [Fact]
    public void CostAsString_ShouldFail()
    {
        var json = ValidJson();
        json["estimatedTotalCostLkr"] = "500";

        ShouldReject(json);
    }

    [Fact]
    public void ReviewFlagAsString_ShouldFail()
    {
        var json = ValidJson();
        json["requiresUserReview"] = "true";

        ShouldReject(json);
    }

    [Fact]
    public void ArrivalTimeAsNumber_ShouldFail()
    {
        var json = ValidJson();

        var stop = json["days"]!.AsArray()[0]!
            ["stops"]!.AsArray()[0]!.AsObject();

        stop["arrivalTime"] = 1030;

        ShouldReject(json);
    }

    [Fact]
    public void DateAsNumber_ShouldFail()
    {
        var json = ValidJson();
        var day = json["days"]!.AsArray()[0]!.AsObject();

        day["date"] = 20261120;

        ShouldReject(json);
    }
}
