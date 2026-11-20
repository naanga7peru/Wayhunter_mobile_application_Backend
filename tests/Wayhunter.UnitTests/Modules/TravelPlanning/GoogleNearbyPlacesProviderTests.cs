
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Wayhunter.Infrastructure.Integrations.Maps;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GoogleNearbyPlacesProviderTests
{
    private const string ValidJson = """
    {
      "places": [
        {
          "id": "google-test-id",
          "displayName": { "text": "Jaffna Restaurant" },
          "location": {
            "latitude": 9.661,
            "longitude": 80.021
          }
        }
      ]
    }
    """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Key { get; private set; }
        public string? Body { get; private set; }
        public string? Url { get; private set; }

        public HttpStatusCode Status { get; set; } =
            HttpStatusCode.OK;

        public string Json { get; set; } = ValidJson;

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            Calls++;
            Url = request.RequestUri!.ToString();

            Key = string.Join(
                "",
                request.Headers.GetValues("X-Goog-Api-Key"));

            Body = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(
                    Json,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private static GoogleNearbyPlacesProvider Create(
        FakeHandler handler,
        bool enabled = true)
    {
        return new GoogleNearbyPlacesProvider(
            new HttpClient(handler),
            Options.Create(new MapsProviderOptions
            {
                EnablePlaces = enabled,
                PlacesServerApiKey = "fake-unit-test-key"
            }));
    }

    [Fact]
    public async Task ValidResult_ReturnsGooglePlace()
    {
        var handler = new FakeHandler();
        var provider = Create(handler);

        var result = await provider.SearchNearbyAsync(
            9.66, 80.02, 1000, "restaurant");

        Assert.True(result.IsSuccess);

        var place = Assert.Single(result.Places);

        Assert.Equal("google-test-id", place.ProviderPlaceId);
        Assert.Equal("GooglePlaces", place.ProviderName);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("fake-unit-test-key", handler.Key);
        Assert.Contains("\"radius\":1000", handler.Body!);
        Assert.Contains("\"restaurant\"", handler.Body!);
        Assert.DoesNotContain("fake-unit-test-key", handler.Body!);
        Assert.DoesNotContain("fake-unit-test-key", handler.Url!);
    }

    [Fact]
    public async Task DisabledProvider_DoesNotCallGoogle()
    {
        var handler = new FakeHandler();
        var provider = Create(handler, enabled: false);

        var result = await provider.SearchNearbyAsync(
            9.66, 80.02, 1000, "hospital");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Places);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task InvalidRadius_IsRejected()
    {
        var handler = new FakeHandler();
        var provider = Create(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.SearchNearbyAsync(
                9.66, 80.02, 5000, "restaurant"));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ProviderFailure_ReturnsNoFakePlaces()
    {
        var handler = new FakeHandler
        {
            Status = HttpStatusCode.ServiceUnavailable
        };

        var provider = Create(handler);

        var result = await provider.SearchNearbyAsync(
            9.66, 80.02, 1000, "restaurant");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Places);
    }

    [Fact]
    public async Task MalformedJson_ReturnsFailure()
    {
        var handler = new FakeHandler
        {
            Json = "not-json"
        };

        var provider = Create(handler);

        var result = await provider.SearchNearbyAsync(
            9.66, 80.02, 1000, "atm");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Places);
    }
}
