
using System;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class WeatherFreshnessEvaluatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan MaximumAge =
        TimeSpan.FromMinutes(30);

    private static WeatherObservation CreateObservation(
        DateTimeOffset observedAt)
    {
        return new WeatherObservation(
            9.66,
            80.02,
            29.0,
            "Sunny",
            observedAt,
            "TestWeatherProvider");
    }

    [Fact]
    public void FreshWeather_ShouldNotBeStale()
    {
        var observation =
            CreateObservation(Now.AddMinutes(-10));

        var result = WeatherFreshnessEvaluator.Evaluate(
            observation,
            Now.AddMinutes(-5),
            Now,
            MaximumAge);

        Assert.False(result.IsStale);
        Assert.Equal(observation, result.Observation);
    }

    [Fact]
    public void OldObservation_ShouldBeStale()
    {
        var observation =
            CreateObservation(Now.AddMinutes(-45));

        var result = WeatherFreshnessEvaluator.Evaluate(
            observation,
            Now.AddMinutes(-5),
            Now,
            MaximumAge);

        Assert.True(result.IsStale);
    }

    [Fact]
    public void OldCache_ShouldBeStale()
    {
        var observation =
            CreateObservation(Now.AddMinutes(-10));

        var result = WeatherFreshnessEvaluator.Evaluate(
            observation,
            Now.AddMinutes(-40),
            Now,
            MaximumAge);

        Assert.True(result.IsStale);
    }

    [Fact]
    public void ExactlyMaximumAge_ShouldRemainFresh()
    {
        var observation =
            CreateObservation(Now.AddMinutes(-30));

        var result = WeatherFreshnessEvaluator.Evaluate(
            observation,
            Now.AddMinutes(-30),
            Now,
            MaximumAge);

        Assert.False(result.IsStale);
    }

    [Fact]
    public void FutureCacheTimestamp_ShouldThrow()
    {
        var observation = CreateObservation(Now);

        Assert.Throws<ArgumentException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                observation,
                Now.AddMinutes(5),
                Now,
                MaximumAge));
    }

    [Fact]
    public void FutureObservation_ShouldThrow()
    {
        var observation =
            CreateObservation(Now.AddMinutes(5));

        Assert.Throws<ArgumentException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                observation,
                Now,
                Now,
                MaximumAge));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveMaximumAge_ShouldThrow(
        int minutes)
    {
        var observation = CreateObservation(Now);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                observation,
                Now,
                Now,
                TimeSpan.FromMinutes(minutes)));
    }

    [Theory]
    [InlineData(91)]
    [InlineData(-91)]
    [InlineData(double.NaN)]
    public void InvalidLatitude_ShouldThrow(
        double latitude)
    {
        var observation = CreateObservation(Now) with
        {
            Latitude = latitude
        };

        Assert.Throws<ArgumentException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                observation,
                Now,
                Now,
                MaximumAge));
    }

    [Theory]
    [InlineData(181)]
    [InlineData(-181)]
    [InlineData(double.NaN)]
    public void InvalidLongitude_ShouldThrow(
        double longitude)
    {
        var observation = CreateObservation(Now) with
        {
            Longitude = longitude
        };

        Assert.Throws<ArgumentException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                observation,
                Now,
                Now,
                MaximumAge));
    }

    [Fact]
    public void InvalidTemperature_ShouldThrow()
    {
        var observation = CreateObservation(Now) with
        {
            TemperatureCelsius = double.NaN
        };

        Assert.Throws<ArgumentException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                observation,
                Now,
                Now,
                MaximumAge));
    }

    [Fact]
    public void MissingProviderName_ShouldThrow()
    {
        var observation = CreateObservation(Now) with
        {
            ProviderName = ""
        };

        Assert.Throws<ArgumentException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                observation,
                Now,
                Now,
                MaximumAge));
    }

    [Fact]
    public void MissingCondition_ShouldThrow()
    {
        var observation = CreateObservation(Now) with
        {
            Condition = " "
        };

        Assert.Throws<ArgumentException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                observation,
                Now,
                Now,
                MaximumAge));
    }

    [Fact]
    public void NullObservation_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WeatherFreshnessEvaluator.Evaluate(
                null!,
                Now,
                Now,
                MaximumAge));
    }
}
