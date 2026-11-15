
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Weather;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class OpenWeatherMapProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastUrl { get; private set; }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Json { get; set; } = "{}";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastUrl = request.RequestUri?.ToString();

            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Json)
            });
        }
    }

    private static WeatherProviderOptions Options() => new()
    {
        Enabled = true,
        ApiKey = "fake-test-key",
        CacheMaxAgeMinutes = 30,
        TimeoutSeconds = 15
    };

    private static string ValidJson()
    {
        var timestamp = DateTimeOffset.UtcNow
            .AddMinutes(-5)
            .ToUnixTimeSeconds();

        return
            "{\"main\":{\"temp\":29.5}," +
            "\"weather\":[{\"main\":\"Clouds\"}]," +
            "\"dt\":" + timestamp + "}";
    }

    [Fact]
    public async Task ValidWeather_ShouldReturnObservation()
    {
        var handler = new FakeHandler { Json = ValidJson() };
        using var client = new HttpClient(handler);

        var provider = new OpenWeatherMapProvider(
            client, Options());

        var result = await provider.GetCurrentWeatherAsync(
            9.66, 80.02);

        Assert.True(result.IsSuccess);

        Assert.NotNull(result.Observation);
        Assert.Equal(29.5, result.Observation.TemperatureCelsius);
        Assert.Equal("Clouds", result.Observation.Condition);
        Assert.Equal("OpenWeatherMap", result.Observation.ProviderName);

        Assert.Contains("units=metric", handler.LastUrl);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Unauthorized_ShouldReturnFailure()
    {
        var handler = new FakeHandler
        {
            Status = HttpStatusCode.Unauthorized
        };

        using var client = new HttpClient(handler);
        var provider = new OpenWeatherMapProvider(
            client, Options());

        var result = await provider.GetCurrentWeatherAsync(
            9.66, 80.02);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Observation);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task InvalidJson_ShouldReturnFailure()
    {
        var handler = new FakeHandler
        {
            Json = "{invalid-json"
        };

        using var client = new HttpClient(handler);
        var provider = new OpenWeatherMapProvider(
            client, Options());

        var result = await provider.GetCurrentWeatherAsync(
            9.66, 80.02);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task DisabledProvider_ShouldNotCallApi()
    {
        var handler = new FakeHandler();
        using var client = new HttpClient(handler);

        var options = Options();
        options.Enabled = false;

        var provider = new OpenWeatherMapProvider(
            client, options);

        var result = await provider.GetCurrentWeatherAsync(
            9.66, 80.02);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task InvalidGps_ShouldRejectBeforeApiCall()
    {
        var handler = new FakeHandler();
        using var client = new HttpClient(handler);

        var provider = new OpenWeatherMapProvider(
            client, Options());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => provider.GetCurrentWeatherAsync(95, 80));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task WeatherCache_ShouldAvoidDuplicateApiCalls()
    {
        var handler = new FakeHandler { Json = ValidJson() };
        using var client = new HttpClient(handler);

        var provider = new OpenWeatherMapProvider(
            client, Options());

        var cache = new WeatherCacheService(
            provider, TimeSpan.FromMinutes(30));

        var first = await cache.GetAsync(9.66, 80.02);
        var second = await cache.GetAsync(9.66, 80.02);

        Assert.Equal(WeatherLookupStatus.Fresh, first.Status);
        Assert.Equal(WeatherLookupStatus.Fresh, second.Status);
        Assert.True(second.FromCache);
        Assert.Equal(1, handler.Calls);
    }
}
