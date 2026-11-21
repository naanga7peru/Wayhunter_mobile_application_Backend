
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class WeatherCacheServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan MaximumAge =
        TimeSpan.FromMinutes(30);

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = Now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan time)
        {
            _now = _now.Add(time);
        }
    }

    private sealed class FakeWeatherProvider : IWeatherProvider
    {
        public int CallCount { get; private set; }

        public WeatherProviderResult Response { get; set; } =
            Success(Now.AddMinutes(-5));

        public Task<WeatherProviderResult> GetCurrentWeatherAsync(
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Response);
        }
    }

    private static WeatherProviderResult Success(
        DateTimeOffset observedAt)
    {
        return new WeatherProviderResult(
            true,
            new WeatherObservation(
                9.66,
                80.02,
                29.0,
                "Sunny",
                observedAt,
                "TestWeatherProvider"),
            null);
    }

    private static WeatherProviderResult Failure() =>
        new(false, null, "Weather provider unavailable");

    [Fact]
    public async Task FirstRequest_ShouldFetchAndCacheWeather()
    {
        var provider = new FakeWeatherProvider();
        var service = new WeatherCacheService(
            provider, MaximumAge, new FakeClock());

        var result = await service.GetAsync(9.66, 80.02);

        Assert.Equal(WeatherLookupStatus.Fresh, result.Status);
        Assert.False(result.FromCache);
        Assert.NotNull(result.Observation);
        Assert.Equal(Now, result.CachedAtUtc);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task FreshCache_ShouldAvoidSecondProviderCall()
    {
        var provider = new FakeWeatherProvider();
        var service = new WeatherCacheService(
            provider, MaximumAge, new FakeClock());

        await service.GetAsync(9.66, 80.02);
        var result = await service.GetAsync(9.66, 80.02);

        Assert.Equal(WeatherLookupStatus.Fresh, result.Status);
        Assert.True(result.FromCache);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ExpiredCache_ShouldFetchFreshWeather()
    {
        var clock = new FakeClock();
        var provider = new FakeWeatherProvider();
        var service = new WeatherCacheService(
            provider, MaximumAge, clock);

        await service.GetAsync(9.66, 80.02);

        clock.Advance(TimeSpan.FromMinutes(31));
        provider.Response = Success(Now.AddMinutes(26));

        var result = await service.GetAsync(9.66, 80.02);

        Assert.Equal(WeatherLookupStatus.Fresh, result.Status);
        Assert.False(result.FromCache);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task ProviderFailure_WithOldCache_ShouldReturnStale()
    {
        var clock = new FakeClock();
        var provider = new FakeWeatherProvider();
        var service = new WeatherCacheService(
            provider, MaximumAge, clock);

        await service.GetAsync(9.66, 80.02);

        clock.Advance(TimeSpan.FromMinutes(40));
        provider.Response = Failure();

        var result = await service.GetAsync(9.66, 80.02);

        Assert.Equal(WeatherLookupStatus.Stale, result.Status);
        Assert.True(result.FromCache);
        Assert.NotNull(result.Observation);
        Assert.Equal(Now.AddMinutes(-5), result.Observation.ObservedAtUtc);
        Assert.Equal(Now, result.CachedAtUtc);
        Assert.NotNull(result.FailureReason);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task ProviderFailure_ObservationExpiresAfter24Hours()
    {
        var clock = new FakeClock();
        var provider = new FakeWeatherProvider();
        var service = new WeatherCacheService(provider, MaximumAge, clock);
        await service.GetAsync(9.66, 80.02);
        provider.Response = Failure();

        clock.Advance(TimeSpan.FromHours(24) - TimeSpan.FromMinutes(5));
        var boundary = await service.GetAsync(9.66, 80.02);
        Assert.Equal(WeatherLookupStatus.Stale, boundary.Status);
        Assert.Equal(Now, boundary.CachedAtUtc);

        clock.Advance(TimeSpan.FromSeconds(1));
        var expired = await service.GetAsync(9.66, 80.02);
        Assert.Equal(WeatherLookupStatus.Unavailable, expired.Status);
        Assert.Null(expired.Observation);
        Assert.Null(expired.CachedAtUtc);
        Assert.False(expired.FromCache);
    }

    [Fact]
    public async Task ProviderFailure_WithoutCache_ShouldBeUnavailable()
    {
        var provider = new FakeWeatherProvider
        {
            Response = Failure()
        };

        var service = new WeatherCacheService(
            provider, MaximumAge, new FakeClock());

        var result = await service.GetAsync(9.66, 80.02);

        Assert.Equal(
            WeatherLookupStatus.Unavailable,
            result.Status);

        Assert.Null(result.Observation);
        Assert.False(result.FromCache);
    }

    [Fact]
    public async Task OldProviderObservation_ShouldBeMarkedStale()
    {
        var provider = new FakeWeatherProvider
        {
            Response = Success(Now.AddMinutes(-60))
        };

        var service = new WeatherCacheService(
            provider, MaximumAge, new FakeClock());

        var result = await service.GetAsync(9.66, 80.02);

        Assert.Equal(WeatherLookupStatus.Stale, result.Status);
        Assert.False(result.FromCache);
    }

    [Theory]
    [InlineData(91, 80)]
    [InlineData(-91, 80)]
    [InlineData(9, 181)]
    [InlineData(9, -181)]
    public async Task InvalidGps_ShouldRejectBeforeProviderCall(
        double latitude,
        double longitude)
    {
        var provider = new FakeWeatherProvider();
        var service = new WeatherCacheService(
            provider, MaximumAge, new FakeClock());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetAsync(latitude, longitude));

        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task InvalidProviderResponse_ShouldThrow()
    {
        var provider = new FakeWeatherProvider
        {
            Response = new WeatherProviderResult(
                true, null, null)
        };

        var service = new WeatherCacheService(
            provider, MaximumAge, new FakeClock());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAsync(9.66, 80.02));
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotCallProvider()
    {
        var provider = new FakeWeatherProvider();
        var service = new WeatherCacheService(
            provider, MaximumAge, new FakeClock());

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetAsync(
                9.66, 80.02, cancellation.Token));

        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidMaximumAge_ShouldThrow(int minutes)
    {
        var provider = new FakeWeatherProvider();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WeatherCacheService(
                provider,
                TimeSpan.FromMinutes(minutes),
                new FakeClock()));
    }
}
