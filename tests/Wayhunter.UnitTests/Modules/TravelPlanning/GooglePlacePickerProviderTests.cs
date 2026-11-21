using Xunit;
using System.Net;
using Microsoft.Extensions.Options;
using Wayhunter.Infrastructure.Integrations.Maps;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GooglePlacePickerProviderTests
{
    private const string Place =
        """{"id":"place_1","displayName":{"text":"Jaffna"},"location":{"latitude":9.66,"longitude":80.02}}""";

    [Fact]
    public async Task Search_SendsTextAndReturnsProviderCoordinates()
    {
        using var client = new HttpClient(new StubHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/places:searchText", request.RequestUri!.AbsolutePath);
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("X-Goog-Api-Key")));
            Assert.DoesNotContain("test-key", request.RequestUri.AbsoluteUri);
            Assert.Contains("Jaffna", await request.Content!.ReadAsStringAsync(token));
            return Respond(HttpStatusCode.OK, "{\"places\":[" + Place + "]}");
        }));

        var result = await CreateProvider(client).SearchAsync("Jaffna");

        Assert.True(result.IsSuccess);
        var place = Assert.Single(result.Places);
        Assert.Equal("place_1", place.ProviderPlaceId);
        Assert.Equal(9.66, place.Latitude);
    }

    [Theory]
    [InlineData("place_1", true)]
    [InlineData("place_2", false)]
    public async Task Details_RequiresMatchingPlaceId(string id, bool success)
    {
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/v1/places/" + id, request.RequestUri!.AbsolutePath);
            return Task.FromResult(Respond(HttpStatusCode.OK, Place));
        }));

        var result = await CreateProvider(client).GetDetailsAsync(id);

        Assert.Equal(success, result.IsSuccess);
        Assert.Equal(success ? 1 : 0, result.Places.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.OK, "{")]
    [InlineData(HttpStatusCode.OK, "{\"places\":[{}]}")]
    public async Task Search_FailsClosedOnUpstreamError(HttpStatusCode status, string content)
    {
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(Respond(status, content))));

        var result = await CreateProvider(client).SearchAsync("Jaffna");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Places);
    }

    [Fact]
    public async Task Search_PropagatesCallerCancellation()
    {
        using var client = new HttpClient(new StubHandler((_, _) =>
            throw new InvalidOperationException("Cancelled request reached upstream.")));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateProvider(client).SearchAsync("Jaffna", cancel.Token));
    }

    private static GooglePlacePickerProvider CreateProvider(HttpClient client) =>
        new(client, Options.Create(new MapsProviderOptions
        {
            EnablePlaces = true,
            PlacesServerApiKey = "test-key"
        }));

    private static HttpResponseMessage Respond(HttpStatusCode status, string content) =>
        new(status) { Content = new StringContent(content) };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
