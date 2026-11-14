
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class ProviderResultValidatorTests
{
    // PLACES PROVIDER TESTS

    [Fact]
    public void Places_FailureWithFakePlaces_ShouldThrow()
    {
        var places = new List<ExternalPlaceSuggestion>
        {
            new(
                "fake-001",
                "Fake Place",
                9.66,
                80.02,
                "TestProvider")
        };

        var result = new PlacesProviderResult(
            false,
            places,
            "Provider unavailable");

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    [Fact]
    public void Places_FailureWithoutReason_ShouldThrow()
    {
        var result = new PlacesProviderResult(
            false,
            Array.Empty<ExternalPlaceSuggestion>(),
            null);

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    [Fact]
    public void Places_ValidFailure_ShouldPass()
    {
        var result = new PlacesProviderResult(
            false,
            Array.Empty<ExternalPlaceSuggestion>(),
            "Service unavailable");

        ProviderResultValidator.Validate(result);
    }

    [Fact]
    public void Places_SuccessWithFailureReason_ShouldThrow()
    {
        var result = new PlacesProviderResult(
            true,
            Array.Empty<ExternalPlaceSuggestion>(),
            "Unexpected error");

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    // ROUTE PROVIDER TESTS

    [Fact]
    public void Route_FailureWithFakeGeometry_ShouldThrow()
    {
        var geometry = new List<RouteWaypoint>
        {
            new(9.66, 80.02),
            new(9.82, 80.23)
        };

        var result = new RouteProviderResult(
            false,
            geometry,
            null,
            null,
            null,
            "Routing failed");

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    [Fact]
    public void Route_FailureWithFakeDistance_ShouldThrow()
    {
        var result = new RouteProviderResult(
            false,
            Array.Empty<RouteWaypoint>(),
            25.5,
            null,
            null,
            "Route unavailable");

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    [Fact]
    public void Route_ValidFailure_ShouldPass()
    {
        var result = new RouteProviderResult(
            false,
            Array.Empty<RouteWaypoint>(),
            null,
            null,
            null,
            "No route found");

        ProviderResultValidator.Validate(result);
    }

    [Fact]
    public void Route_SuccessWithoutValidGeometry_ShouldThrow()
    {
        var result = new RouteProviderResult(
            true,
            new List<RouteWaypoint>
            {
                new(9.66, 80.02)
            },
            10.0,
            20.0,
            "TestProvider",
            null);

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    [Fact]
    public void Route_NegativeDistance_ShouldThrow()
    {
        var result = new RouteProviderResult(
            true,
            new List<RouteWaypoint>
            {
                new(9.66, 80.02),
                new(9.82, 80.23)
            },
            -10.0,
            20.0,
            "TestProvider",
            null);

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    [Fact]
    public void Route_ValidSuccess_ShouldPass()
    {
        var result = new RouteProviderResult(
            true,
            new List<RouteWaypoint>
            {
                new(9.66, 80.02),
                new(9.82, 80.23)
            },
            25.5,
            40.0,
            "TestProvider",
            null);

        ProviderResultValidator.Validate(result);
    }

    // WEATHER PROVIDER TESTS

    [Fact]
    public void Weather_FailureWithFakeObservation_ShouldThrow()
    {
        var observation = new WeatherObservation(
            9.66,
            80.02,
            29.0,
            "Sunny",
            DateTimeOffset.UtcNow,
            "TestProvider");

        var result = new WeatherProviderResult(
            false,
            observation,
            "Weather API failed");

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    [Fact]
    public void Weather_ValidFailure_ShouldPass()
    {
        var result = new WeatherProviderResult(
            false,
            null,
            "Weather provider unavailable");

        ProviderResultValidator.Validate(result);
    }

    [Fact]
    public void Weather_SuccessWithoutObservation_ShouldThrow()
    {
        var result = new WeatherProviderResult(
            true,
            null,
            null);

        Assert.Throws<InvalidOperationException>(
            () => ProviderResultValidator.Validate(result));
    }

    [Fact]
    public void Weather_ValidSuccess_ShouldPass()
    {
        var observation = new WeatherObservation(
            9.66,
            80.02,
            29.0,
            "Cloudy",
            DateTimeOffset.UtcNow,
            "TestProvider");

        var result = new WeatherProviderResult(
            true,
            observation,
            null);

        ProviderResultValidator.Validate(result);
    }
}
