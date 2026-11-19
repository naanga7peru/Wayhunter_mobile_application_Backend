
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GenerateAiTripPlanServiceTests
{
    private const string ValidJson = """
    {
      "days": [
        {
          "date": "2026-11-20",
          "stops": [
            {
              "placeName": "Nallur",
              "providerPlaceId": null,
              "arrivalTime": null,
              "estimatedCostLkr": 500
            }
          ]
        }
      ],
      "estimatedTotalCostLkr": 500,
      "requiresUserReview": true
    }
    """;

    private sealed class FakeAiProvider
        : IAiTripGenerationProvider
    {
        public string Json { get; set; } = ValidJson;
        public int Calls { get; private set; }

        public Task<string> GenerateItineraryJsonAsync(
            AiTripItineraryRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Json);
        }
    }

    private static AiTripItineraryRequest ValidRequest() =>
        new(
            "Jaffna",
            "Point Pedro",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 21),
            20000m,
            "Car",
            2,
            null);

    [Fact]
    public async Task ValidAiOutput_ReturnsReviewablePlan()
    {
        var provider = new FakeAiProvider();
        var service = new GenerateAiTripPlanService(provider);

        var result = await service.GenerateAsync(ValidRequest());

        Assert.True(result.RequiresUserReview);
        Assert.Single(result.Days);
        Assert.Equal(500m, result.EstimatedTotalCostLkr);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task InvalidRequest_DoesNotCallProvider()
    {
        var provider = new FakeAiProvider();
        var service = new GenerateAiTripPlanService(provider);

        var request = ValidRequest() with
        {
            StartLocation = ""
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateAsync(request));

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task MalformedAiJson_IsRejected()
    {
        var provider = new FakeAiProvider
        {
            Json = "{ invalid json"
        };

        var service = new GenerateAiTripPlanService(provider);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateAsync(ValidRequest()));
    }

    [Fact]
    public async Task MissingUserReviewFlag_IsRejected()
    {
        var provider = new FakeAiProvider
        {
            Json = ValidJson.Replace(
                "\"requiresUserReview\": true",
                "\"requiresUserReview\": false")
        };

        var service = new GenerateAiTripPlanService(provider);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateAsync(ValidRequest()));
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCallProvider()
    {
        var provider = new FakeAiProvider();
        var service = new GenerateAiTripPlanService(provider);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GenerateAsync(
                ValidRequest(),
                cts.Token));

        Assert.Equal(0, provider.Calls);
    }
}
