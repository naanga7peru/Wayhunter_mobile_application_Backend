
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class PreviewTripRouteServiceTests
{
    private sealed class FakeRoutesProvider : IRoutesProvider
    {
        public int Calls { get; private set; }

        public RouteProviderResult Response { get; set; } =
            new(
                true,
                new[]
                {
                    new RouteWaypoint(9.660, 80.020),
                    new RouteWaypoint(9.670, 80.030)
                },
                2.5,
                5.0,
                "TestRoutes",
                null);

        public Task<RouteProviderResult> CalculateRouteAsync(
            IReadOnlyList<RouteWaypoint> orderedStops,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Response);
        }
    }

    private static RouteWaypoint[] ValidStops() =>
    [
        new(9.660, 80.020),
        new(9.670, 80.030)
    ];

    [Fact]
    public async Task ValidRoute_ReturnsGeometryAndEstimates()
    {
        var fake = new FakeRoutesProvider();
        var service = new PreviewTripRouteService(fake);

        var result = await service.PreviewAsync(ValidStops());

        Assert.True(result.IsSuccess);
        Assert.Equal(2.5, result.DistanceKm);
        Assert.Equal(5.0, result.EstimatedDurationMinutes);
        Assert.Equal(2, result.RouteGeometry.Count);
        Assert.Equal("TestRoutes", result.ProviderName);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task ProviderFailure_DoesNotInventRoute()
    {
        var fake = new FakeRoutesProvider
        {
            Response = new RouteProviderResult(
                false,
                Array.Empty<RouteWaypoint>(),
                null,
                null,
                null,
                "Provider unavailable")
        };

        var service = new PreviewTripRouteService(fake);

        var result = await service.PreviewAsync(ValidStops());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.RouteGeometry);
        Assert.Null(result.DistanceKm);
        Assert.Null(result.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task OneStop_IsRejectedBeforeProviderCall()
    {
        var fake = new FakeRoutesProvider();
        var service = new PreviewTripRouteService(fake);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PreviewAsync(
            [
                new RouteWaypoint(9.66, 80.02)
            ]));

        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task MoreThan27Stops_IsRejected()
    {
        var fake = new FakeRoutesProvider();
        var service = new PreviewTripRouteService(fake);

        var stops = Enumerable.Range(0, 28)
            .Select(i => new RouteWaypoint(
                9.66 + i * 0.001,
                80.02))
            .ToArray();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PreviewAsync(stops));

        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData(91, 80)]
    [InlineData(9.66, 181)]
    public async Task InvalidGps_IsRejected(
        double latitude,
        double longitude)
    {
        var fake = new FakeRoutesProvider();
        var service = new PreviewTripRouteService(fake);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PreviewAsync(
            [
                new RouteWaypoint(latitude, longitude),
                new RouteWaypoint(9.67, 80.03)
            ]));

        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task NullStops_IsRejected()
    {
        var fake = new FakeRoutesProvider();
        var service = new PreviewTripRouteService(fake);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PreviewAsync(null!));

        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task InvalidProviderGeometry_IsRejected()
    {
        var fake = new FakeRoutesProvider
        {
            Response = new RouteProviderResult(
                true,
                new[]
                {
                    new RouteWaypoint(9.66, 80.02),
                    new RouteWaypoint(100, 80.03)
                },
                2.5,
                5,
                "TestRoutes",
                null)
        };

        var service = new PreviewTripRouteService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PreviewAsync(ValidStops()));
    }

    [Fact]
    public async Task MissingEta_IsRejected()
    {
        var fake = new FakeRoutesProvider
        {
            Response = new RouteProviderResult(
                true,
                ValidStops(),
                2.5,
                null,
                "TestRoutes",
                null)
        };

        var service = new PreviewTripRouteService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PreviewAsync(ValidStops()));
    }

    [Fact]
    public async Task NonFiniteDistance_IsRejected()
    {
        var fake = new FakeRoutesProvider
        {
            Response = new RouteProviderResult(
                true,
                ValidStops(),
                double.NaN,
                5,
                "TestRoutes",
                null)
        };

        var service = new PreviewTripRouteService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PreviewAsync(ValidStops()));
    }

    [Fact]
    public async Task InvalidFailureResult_IsRejected()
    {
        var fake = new FakeRoutesProvider
        {
            Response = new RouteProviderResult(
                false,
                Array.Empty<RouteWaypoint>(),
                2.5,
                null,
                null,
                "Provider unavailable")
        };

        var service = new PreviewTripRouteService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PreviewAsync(ValidStops()));
    }

    [Fact]
    public async Task Cancellation_StopsBeforeProviderCall()
    {
        var fake = new FakeRoutesProvider();
        var service = new PreviewTripRouteService(fake);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.PreviewAsync(
                ValidStops(),
                cts.Token));

        Assert.Equal(0, fake.Calls);
    }
}
