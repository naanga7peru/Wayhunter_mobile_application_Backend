using System.Net;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Maps;
using Wayhunter.Infrastructure.Integrations.Weather;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class WeatherForecastAndQuotaTests
{
    [Fact]
    public async Task ForecastCache_FiltersExpiredPointsAndUsesBoundedStaleFallback()
    {
        var clock = new TestClock();
        var provider = new TestForecast([
            new(clock.Now.AddMinutes(-1), 25, "Clouds"),
            new(clock.Now.AddHours(1), 26, "Clear"),
            new(clock.Now.AddHours(36), 27, "Rain")
        ]);
        var service = new WeatherForecastService(provider, TimeSpan.FromMinutes(30), clock);

        var first = await service.GetAsync(9, 80, default);
        Assert.NotNull(first);
        Assert.Equal("fresh", first.Status);
        Assert.Equal(2, first.Forecasts.Count);
        await service.GetAsync(9, 80, default);
        Assert.Equal(1, provider.Calls);

        provider.Points = null;
        clock.Now = clock.Now.AddHours(2);
        var stale = await service.GetAsync(9, 80, default);
        Assert.NotNull(stale);
        Assert.Equal("stale", stale.Status);
        Assert.Single(stale.Forecasts);

        clock.Now = clock.Now.AddHours(23);
        Assert.Null(await service.GetAsync(9, 80, default));
    }

    [Fact]
    public async Task ForecastCache_EvictsAtCapacity()
    {
        var clock = new TestClock();
        var provider = new TestForecast([new(clock.Now.AddHours(2), 25, "Clear")]);
        var service = new WeatherForecastService(provider, TimeSpan.FromMinutes(30), clock);
        for (var i = 0; i < 257; i++)
        {
            clock.Now = clock.Now.AddSeconds(1);
            Assert.NotNull(await service.GetAsync(9, 80 + i / 1000d, default));
        }

        Assert.Equal(257, provider.Calls);
        Assert.NotNull(await service.GetAsync(9, 80, default));
        Assert.Equal(258, provider.Calls);
    }

    [Theory]
    [InlineData("{\"list\":[{\"dt\":1893456000,\"main\":{\"temp\":29},\"weather\":[{\"main\":\"Rain\"}]}]}", true)]
    [InlineData("{\"list\":[{\"dt\":1893456000,\"main\":{\"temp\":29},\"weather\":[]}]}", false)]
    [InlineData("{\"list\":[null]}", false)]
    [InlineData("{", false)]
    public async Task OpenWeatherForecast_ValidatesUpstreamShape(string payload, bool valid)
    {
        using var http = new HttpClient(new WeatherHandler(payload));
        var provider = new OpenWeatherMapProvider(http, new WeatherProviderOptions
        {
            Enabled = true, ApiKey = "test-weather-key"
        });

        var points = await provider.GetForecastAsync(9, 80);
        if (valid)
        {
            Assert.NotNull(points);
            var point = Assert.Single(points);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1893456000), point.ForecastAtUtc);
            Assert.Equal(29, point.TemperatureCelsius);
            Assert.Equal("Rain", point.Condition);
        }
        else
        {
            Assert.Null(points);
        }
    }

    [Fact]
    public void Quota_ConcurrencyCapacityAndUtcRolloverCannotResetActiveUser()
    {
        var clock = new TestClock();
        var quota = new InMemoryTravelProviderQuota(clock, maxUsers: 1);
        var first = Guid.NewGuid().ToString();
        var second = Guid.NewGuid().ToString();
        var granted = 0;

        Parallel.For(0, 100, _ =>
        {
            if (quota.TryAcquire(first, 20))
                Interlocked.Increment(ref granted);
        });

        Assert.Equal(20, granted);
        Assert.False(quota.TryAcquire(second, 20));
        Assert.False(quota.TryAcquire(first, 20));
        clock.Now = clock.Now.AddDays(1);
        Assert.True(quota.TryAcquire(second, 20));
        clock.Now = clock.Now.AddDays(-1);
        Assert.False(quota.TryAcquire(first, 20));
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestForecast(IReadOnlyList<WeatherForecastPoint>? points) : IWeatherForecastProvider
    {
        public IReadOnlyList<WeatherForecastPoint>? Points { get; set; } = points;
        public int Calls { get; private set; }

        public Task<IReadOnlyList<WeatherForecastPoint>?> GetForecastAsync(
            double latitude, double longitude, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Points);
        }
    }

    private sealed class WeatherHandler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/data/2.5/forecast", request.RequestUri!.AbsolutePath);
            Assert.Contains("units=metric", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            });
        }
    }
}
