
using System;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterDuplicateCandidatePipelineTests
{
    private const double EarthRadiusMeters = 6371000.0;

    private static ExistingVerifiedPlace CreatePlace(
        string name,
        double distanceMeters)
    {
        double longitude =
            distanceMeters / EarthRadiusMeters * 180.0 / Math.PI;

        return new ExistingVerifiedPlace(
            Guid.NewGuid(),
            name,
            0,
            longitude);
    }

    [Fact]
    public async Task Pipeline_ShouldReturnNearestCandidates()
    {
        var near = CreatePlace("Near", 50);
        var middle = CreatePlace("Middle", 150);
        var far = CreatePlace("Far", 250);

        var reader = new InMemoryHunterVerifiedPlaceReader(
            new[] { far, middle, near });

        var service = new HunterDuplicateCandidateQueryService(
            reader);

        var result = await service.FindNearbyCandidatesAsync(
            0, 0);

        Assert.Equal(2, result.Count);
        Assert.Equal(near.PlaceId, result[0].PlaceId);
        Assert.Equal(middle.PlaceId, result[1].PlaceId);
    }

    [Fact]
    public async Task Pipeline_ShouldExcludePlacesOutsideRadius()
    {
        var reader = new InMemoryHunterVerifiedPlaceReader(
            new[]
            {
                CreatePlace("Outside A", 200.1),
                CreatePlace("Outside B", 300)
            });

        var service = new HunterDuplicateCandidateQueryService(
            reader);

        var result = await service.FindNearbyCandidatesAsync(
            0, 0);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Pipeline_ShouldIncludePlaceJustInsideRadius()
    {
        var nearby = CreatePlace("Nearby", 199.9);

        var reader = new InMemoryHunterVerifiedPlaceReader(
            new[] { nearby });

        var service = new HunterDuplicateCandidateQueryService(
            reader);

        var result = await service.FindNearbyCandidatesAsync(
            0, 0);

        Assert.Single(result);
        Assert.Equal(nearby.PlaceId, result[0].PlaceId);
    }

    [Fact]
    public async Task Pipeline_ShouldIncludeSameLocation()
    {
        var same = CreatePlace("Same Location", 0);

        var reader = new InMemoryHunterVerifiedPlaceReader(
            new[] { same });

        var service = new HunterDuplicateCandidateQueryService(
            reader);

        var result = await service.FindNearbyCandidatesAsync(
            0, 0);

        Assert.Single(result);
        Assert.Equal(0, result[0].DistanceMeters, 6);
    }
}
