
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterDuplicateCandidateRulesTests
{
    // Test positions are on the equator for predictable distances.
    private const double EarthRadiusMeters = 6371000.0;

    private static double LongitudeOffset(double meters)
    {
        return meters / EarthRadiusMeters * 180.0 / Math.PI;
    }

    [Fact]
    public void SameLocation_ShouldHaveZeroDistance()
    {
        var distance =
            HunterDuplicateCandidateRules.CalculateDistanceMeters(
                9.6615, 80.0087,
                9.6615, 80.0087);

        Assert.Equal(0.0, distance, 6);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(199)]
    public void Within200Meters_ShouldBePotentialDuplicate(
        double meters)
    {
        var result =
            HunterDuplicateCandidateRules.IsPotentialDuplicate(
                0, 0,
                0, LongitudeOffset(meters));

        Assert.True(result);
    }

    [Theory]
    [InlineData(201)]
    [InlineData(250)]
    public void Beyond200Meters_ShouldNotBePotentialDuplicate(
        double meters)
    {
        var result =
            HunterDuplicateCandidateRules.IsPotentialDuplicate(
                0, 0,
                0, LongitudeOffset(meters));

        Assert.False(result);
    }

    [Fact]
    public void CalculatedDistance_ShouldMatchExpectedMeters()
    {
        var distance =
            HunterDuplicateCandidateRules.CalculateDistanceMeters(
                0, 0,
                0, LongitudeOffset(150));

        Assert.InRange(distance, 149.9, 150.1);
    }

    [Fact]
    public void Distance_ShouldBeSymmetric()
    {
        var forward =
            HunterDuplicateCandidateRules.CalculateDistanceMeters(
                9.6615, 80.0087,
                9.6630, 80.0100);

        var reverse =
            HunterDuplicateCandidateRules.CalculateDistanceMeters(
                9.6630, 80.0100,
                9.6615, 80.0087);

        Assert.Equal(forward, reverse, 6);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidLatitude_ShouldThrow(double latitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterDuplicateCandidateRules.CalculateDistanceMeters(
                latitude, 80,
                9.6615, 80.0087));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidLongitude_ShouldThrow(double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterDuplicateCandidateRules.CalculateDistanceMeters(
                9.6615, longitude,
                9.6630, 80.0100));
    }
}
