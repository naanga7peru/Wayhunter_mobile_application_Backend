
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class InMemoryHunterVerifiedPlaceReaderTests
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
    public async Task NearbyPlaces_ShouldBeReturned()
    {
        var near = CreatePlace("Near Place", 75);
        var middle = CreatePlace("Middle Place", 180);
        var far = CreatePlace("Far Place", 350);

        var reader = new InMemoryHunterVerifiedPlaceReader(
            new[] { near, middle, far });

        var results = await reader.GetVerifiedPlacesNearAsync(
            0, 0, 200);

        Assert.Equal(2, results.Count);
        Assert.Contains(results,
            place => place.PlaceId == near.PlaceId);
        Assert.Contains(results,
            place => place.PlaceId == middle.PlaceId);
        Assert.DoesNotContain(results,
            place => place.PlaceId == far.PlaceId);
    }

    [Fact]
    public async Task OutsideRadius_ShouldReturnEmpty()
    {
        var reader = new InMemoryHunterVerifiedPlaceReader(
            new[] { CreatePlace("Far Place", 350) });

        var results = await reader.GetVerifiedPlacesNearAsync(
            0, 0, 200);

        Assert.Empty(results);
    }

    [Fact]
    public async Task EmptyRepository_ShouldReturnEmpty()
    {
        var reader = new InMemoryHunterVerifiedPlaceReader(
            Array.Empty<ExistingVerifiedPlace>());

        var results = await reader.GetVerifiedPlacesNearAsync(
            0, 0, 200);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SameLocation_ShouldBeIncluded()
    {
        var place = CreatePlace("Same Place", 0);

        var reader = new InMemoryHunterVerifiedPlaceReader(
            new[] { place });

        var results = await reader.GetVerifiedPlacesNearAsync(
            0, 0, 200);

        Assert.Single(results);
        Assert.Equal(place.PlaceId, results[0].PlaceId);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task InvalidRadius_ShouldThrow(double radius)
    {
        var reader = new InMemoryHunterVerifiedPlaceReader(
            Array.Empty<ExistingVerifiedPlace>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await reader.GetVerifiedPlacesNearAsync(
                    0, 0, radius));
    }



    [Fact]
    public async Task InvalidGps_ShouldThrow()
    {
        var reader = new InMemoryHunterVerifiedPlaceReader(
            Array.Empty<ExistingVerifiedPlace>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await reader.GetVerifiedPlacesNearAsync(
                    95, 80, 200));
    }


    [Fact]
    public async Task CancelledRequest_ShouldThrow()
    {
        var reader = new InMemoryHunterVerifiedPlaceReader(
            Array.Empty<ExistingVerifiedPlace>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await reader.GetVerifiedPlacesNearAsync(
                    0, 0, 200, cts.Token));
    }

    [Fact]
    public async Task Repository_ShouldKeepInputSnapshot()
    {
        var places = new List<ExistingVerifiedPlace>();

        var reader = new InMemoryHunterVerifiedPlaceReader(
            places);

        places.Add(CreatePlace("Added Later", 50));

        var results = await reader.GetVerifiedPlacesNearAsync(
            0, 0, 200);

        Assert.Empty(results);
    }

    [Fact]
    public void NullPlaces_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InMemoryHunterVerifiedPlaceReader(null!));
    }
}
