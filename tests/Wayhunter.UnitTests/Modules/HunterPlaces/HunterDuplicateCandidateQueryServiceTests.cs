
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Queries;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterDuplicateCandidateQueryServiceTests
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
    public void NullReader_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HunterDuplicateCandidateQueryService(null!));
    }

    [Fact]
    public async Task NearbyPlaces_ShouldReturnNearestCandidates()
    {
        var far = CreatePlace("Far Place", 250);
        var middle = CreatePlace("Middle Place", 150);
        var near = CreatePlace("Near Place", 50);

        var reader = new FakeVerifiedPlaceReader
        {
            Places = new[] { far, middle, near }
        };

        var service =
            new HunterDuplicateCandidateQueryService(reader);

        var result = await service.FindNearbyCandidatesAsync(
            0, 0);

        Assert.Equal(2, result.Count);
        Assert.Equal(near.PlaceId, result[0].PlaceId);
        Assert.Equal(middle.PlaceId, result[1].PlaceId);
    }

    [Fact]
    public async Task EmptyRepository_ShouldReturnEmpty()
    {
        var reader = new FakeVerifiedPlaceReader();

        var service =
            new HunterDuplicateCandidateQueryService(reader);

        var result = await service.FindNearbyCandidatesAsync(
            0, 0);

        Assert.Empty(result);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task Reader_ShouldReceiveCorrectGpsRadiusAndToken()
    {
        var reader = new FakeVerifiedPlaceReader();

        var service =
            new HunterDuplicateCandidateQueryService(reader);

        using var cts = new CancellationTokenSource();

        await service.FindNearbyCandidatesAsync(
            9.6615,
            80.0087,
            cts.Token);

        Assert.Equal(1, reader.CallCount);
        Assert.Equal(9.6615, reader.LastLatitude);
        Assert.Equal(80.0087, reader.LastLongitude);

        Assert.Equal(
            HunterDuplicateCandidateRules.CandidateRadiusMeters,
            reader.LastRadiusMeters);

        Assert.Equal(
            cts.Token,
            reader.LastCancellationToken);
    }

    [Theory]
    [InlineData(91.0, 80.0)]
    [InlineData(9.0, 181.0)]
    public async Task InvalidGps_ShouldNotCallReader(
        double latitude,
        double longitude)
    {
        var reader = new FakeVerifiedPlaceReader();

        var service =
            new HunterDuplicateCandidateQueryService(reader);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await service.FindNearbyCandidatesAsync(
                    latitude,
                    longitude));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotCallReader()
    {
        var reader = new FakeVerifiedPlaceReader();

        var service =
            new HunterDuplicateCandidateQueryService(reader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await service.FindNearbyCandidatesAsync(
                    0, 0, cts.Token));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task ReaderFailure_ShouldPropagate()
    {
        var reader = new FakeVerifiedPlaceReader
        {
            Failure = new InvalidOperationException(
                "Verified place read failed")
        };

        var service =
            new HunterDuplicateCandidateQueryService(reader);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await service.FindNearbyCandidatesAsync(
                        0, 0));

        Assert.Equal(
            "Verified place read failed",
            exception.Message);
    }

    private sealed class FakeVerifiedPlaceReader
        : IHunterVerifiedPlaceReader
    {
        public IReadOnlyList<ExistingVerifiedPlace> Places
        { get; set; } = Array.Empty<ExistingVerifiedPlace>();

        public Exception? Failure { get; set; }

        public int CallCount { get; private set; }

        public double LastLatitude { get; private set; }

        public double LastLongitude { get; private set; }

        public double LastRadiusMeters { get; private set; }

        public CancellationToken LastCancellationToken
        { get; private set; }

        public Task<IReadOnlyList<ExistingVerifiedPlace>>
            GetVerifiedPlacesNearAsync(
                double latitude,
                double longitude,
                double radiusMeters,
                CancellationToken cancellationToken = default)
        {
            CallCount++;

            LastLatitude = latitude;
            LastLongitude = longitude;
            LastRadiusMeters = radiusMeters;
            LastCancellationToken = cancellationToken;

            if (Failure is not null)
            {
                return Task.FromException<
                    IReadOnlyList<ExistingVerifiedPlace>>(
                        Failure);
            }

            return Task.FromResult(Places);
        }
    }
}
