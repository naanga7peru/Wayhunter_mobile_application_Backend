
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class NearbyPlaceSearchServiceTests
{
    private static NearbySearchRequest ValidRequest() =>
        new(9.66, 80.02, NearbyPlaceCategory.Food);

    private static ExternalPlaceSuggestion Place(
        string id,
        string name,
        double latitude,
        double longitude) =>
        new(id, name, latitude, longitude, "TestProvider");

    private sealed class FakeNearbyPlacesProvider
        : INearbyPlacesProvider
    {
        public PlacesProviderResult Response { get; set; } =
            new(true, Array.Empty<ExternalPlaceSuggestion>(), null);

        public int CallCount { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public int RadiusMeters { get; private set; }
        public string? SearchTerm { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task<PlacesProviderResult> SearchNearbyAsync(
            double latitude,
            double longitude,
            int radiusMeters,
            string searchTerm,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Latitude = latitude;
            Longitude = longitude;
            RadiusMeters = radiusMeters;
            SearchTerm = searchTerm;
            ReceivedToken = cancellationToken;

            return Task.FromResult(Response);
        }
    }

    [Fact]
    public async Task NearbyPlaces_ShouldReturnOnlyWithinOneKm()
    {
        var fake = new FakeNearbyPlacesProvider
        {
            Response = new PlacesProviderResult(
                true,
                new[]
                {
                    Place("place-001", "Nearby Restaurant",
                        9.664, 80.02),

                    Place("place-002", "Far Restaurant",
                        9.68, 80.02)
                },
                null)
        };

        var service = new NearbyPlaceSearchService(fake);

        var result = await service.SearchAsync(ValidRequest());

        Assert.True(result.IsSuccess);

        var place = Assert.Single(result.Places);

        Assert.Equal("place-001", place.ProviderPlaceId);
    }

    [Fact]
    public async Task Provider_ShouldReceiveCorrectSearchParameters()
    {
        var fake = new FakeNearbyPlacesProvider();
        var service = new NearbyPlaceSearchService(fake);

        using var cancellation = new CancellationTokenSource();

        await service.SearchAsync(
            ValidRequest(),
            cancellation.Token);

        Assert.Equal(1, fake.CallCount);
        Assert.Equal(9.66, fake.Latitude);
        Assert.Equal(80.02, fake.Longitude);
        Assert.Equal(1000, fake.RadiusMeters);
        Assert.Equal("restaurant", fake.SearchTerm);
        Assert.Equal(
            cancellation.Token,
            fake.ReceivedToken);
    }

    [Fact]
    public async Task ProviderFailure_ShouldReturnFailure()
    {
        var fake = new FakeNearbyPlacesProvider
        {
            Response = new PlacesProviderResult(
                false,
                Array.Empty<ExternalPlaceSuggestion>(),
                "Provider unavailable")
        };

        var service = new NearbyPlaceSearchService(fake);

        var result = await service.SearchAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Places);
        Assert.Equal(
            "Provider unavailable",
            result.FailureReason);
    }

    [Fact]
    public async Task FailedProviderWithFakePlaces_ShouldThrow()
    {
        var fake = new FakeNearbyPlacesProvider
        {
            Response = new PlacesProviderResult(
                false,
                new[]
                {
                    Place("fake-001", "Fake Place",
                        9.66, 80.02)
                },
                "Provider failure")
        };

        var service = new NearbyPlaceSearchService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SearchAsync(ValidRequest()));
    }

    [Fact]
    public async Task InvalidGps_ShouldRejectBeforeProviderCall()
    {
        var fake = new FakeNearbyPlacesProvider();
        var service = new NearbyPlaceSearchService(fake);

        var invalidRequest = ValidRequest() with
        {
            Latitude = 95
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchAsync(invalidRequest));

        Assert.Equal(0, fake.CallCount);
    }

    [Fact]
    public async Task InvalidProviderCoordinates_ShouldBeExcluded()
    {
        var fake = new FakeNearbyPlacesProvider
        {
            Response = new PlacesProviderResult(
                true,
                new[]
                {
                    Place("bad-001", "Invalid GPS",
                        double.NaN, 80.02),

                    Place("good-001", "Valid Place",
                        9.661, 80.02)
                },
                null)
        };

        var service = new NearbyPlaceSearchService(fake);

        var result = await service.SearchAsync(ValidRequest());

        var place = Assert.Single(result.Places);

        Assert.Equal("good-001", place.ProviderPlaceId);
    }

    [Fact]
    public async Task PlaceWithoutId_ShouldBeExcluded()
    {
        var fake = new FakeNearbyPlacesProvider
        {
            Response = new PlacesProviderResult(
                true,
                new[]
                {
                    Place("", "Unnamed Provider ID",
                        9.661, 80.02),

                    Place("good-002", "Valid Place",
                        9.662, 80.02)
                },
                null)
        };

        var service = new NearbyPlaceSearchService(fake);

        var result = await service.SearchAsync(ValidRequest());

        var place = Assert.Single(result.Places);

        Assert.Equal("good-002", place.ProviderPlaceId);
    }

    [Fact]
    public async Task InvalidSuccessResult_ShouldThrow()
    {
        var fake = new FakeNearbyPlacesProvider
        {
            Response = new PlacesProviderResult(
                true,
                Array.Empty<ExternalPlaceSuggestion>(),
                "Unexpected failure reason")
        };

        var service = new NearbyPlaceSearchService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SearchAsync(ValidRequest()));
    }

    [Fact]
    public void NullProvider_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new NearbyPlaceSearchService(null!));
    }
}
