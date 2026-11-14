
using System;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterDuplicateCandidateLookupTests
{
    private const double EarthRadiusMeters = 6371000.0;

    private static double LongitudeOffset(double meters)
    {
        return meters / EarthRadiusMeters * 180.0 / Math.PI;
    }

    private static ExistingVerifiedPlace CreatePlace(
        string name,
        double distanceMeters)
    {
        return new ExistingVerifiedPlace(
            Guid.NewGuid(),
            name,
            0,
            LongitudeOffset(distanceMeters));
    }

    [Fact]
    public void NearbyPlaces_ShouldReturnNearestFirst()
    {
        var far = CreatePlace("Far Place", 250);
        var middle = CreatePlace("Middle Place", 150);
        var near = CreatePlace("Near Place", 50);

        var result = HunterDuplicateCandidateLookup.FindCandidates(
            0, 0,
            new[] { far, middle, near });

        Assert.Equal(2, result.Count);
        Assert.Equal(near.PlaceId, result[0].PlaceId);
        Assert.Equal(middle.PlaceId, result[1].PlaceId);
        Assert.InRange(result[0].DistanceMeters, 49.9, 50.1);
    }

    [Fact]
    public void Outside200Meters_ShouldReturnEmpty()
    {
        var places = new[]
        {
            CreatePlace("Place A", 250),
            CreatePlace("Place B", 350)
        };

        var result = HunterDuplicateCandidateLookup.FindCandidates(
            0, 0, places);

        Assert.Empty(result);
    }

    [Fact]
    public void EmptyPlaces_ShouldReturnEmpty()
    {
        var result = HunterDuplicateCandidateLookup.FindCandidates(
            0, 0,
            Array.Empty<ExistingVerifiedPlace>());

        Assert.Empty(result);
    }

    [Fact]
    public void SameLocation_ShouldBeCandidate()
    {
        var place = CreatePlace("Same Place", 0);

        var result = HunterDuplicateCandidateLookup.FindCandidates(
            0, 0, new[] { place });

        Assert.Single(result);
        Assert.Equal(0.0, result[0].DistanceMeters, 6);
    }

    [Fact]
    public void JustInside200Meters_ShouldBeCandidate()
    {
        var place = CreatePlace("Nearby Place", 199.9);

        var result = HunterDuplicateCandidateLookup.FindCandidates(
            0, 0, new[] { place });

        Assert.Single(result);
    }

    [Fact]
    public void JustOutside200Meters_ShouldNotBeCandidate()
    {
        var place = CreatePlace("Distant Place", 200.1);

        var result = HunterDuplicateCandidateLookup.FindCandidates(
            0, 0, new[] { place });

        Assert.Empty(result);
    }

    [Fact]
    public void InvalidNewCoordinates_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterDuplicateCandidateLookup.FindCandidates(
                91, 80,
                Array.Empty<ExistingVerifiedPlace>()));
    }

    [Fact]
    public void InvalidExistingCoordinates_ShouldThrow()
    {
        var invalidPlace = new ExistingVerifiedPlace(
            Guid.NewGuid(),
            "Invalid Place",
            95,
            80);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterDuplicateCandidateLookup.FindCandidates(
                9.6615, 80.0087,
                new[] { invalidPlace }));
    }

    [Fact]
    public void NullPlaces_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HunterDuplicateCandidateLookup.FindCandidates(
                0, 0, null!));
    }
}
