
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Infrastructure.Integrations.Maps;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GoogleRoutesProviderTests
{
    // Google's standard example encoded polyline.
    private const string ValidResponse = """
    {
      "routes": [
        {
          "distanceMeters": 2500,
          "duration": "150s",
          "polyline": {
            "encodedPolyline": "_p~iF~ps|U_ulLnnqC_mqNvxq`@"
          }
        }
      ]
    }
    """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Url { get; private set; }
        public string? Body { get; private set; }
        public string? ApiKey { get; private set; }
        public string? FieldMask { get; private set; }
        public HttpMethod? Method { get; private set; }

        public HttpStatusCode Status { get; set; } =
            HttpStatusCode.OK;

        public string ResponseBody { get; set; } =
            ValidResponse;

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            Calls++;

            Url = request.RequestUri?.ToString();
            Method = request.Method;

            ApiKey = string.Join(
                "",
                request.Headers.GetValues("X-Goog-Api-Key"));

            FieldMask = string.Join(
                "",
                request.Headers.GetValues("X-Goog-FieldMask"));

            Body = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(
                    ResponseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private static GoogleRoutesProvider Create(
        FakeHandler handler,
        bool enabled = true)
    {
        var options = Options.Create(
            new MapsProviderOptions
            {
                EnableRoutes = enabled,
                RoutesServerApiKey = "dummy-test-routes-key"
            });

        return new GoogleRoutesProvider(
            new HttpClient(handler),
            options);
    }

    private static RouteWaypoint[] ValidStops() =>
    [
        new(9.660, 80.020),
        new(9.665, 80.025),
        new(9.670, 80.030)
    ];

    [Fact]
    public async Task ValidResponse_ReturnsDistanceDurationAndGeometry()
    {
        var handler = new FakeHandler();
        var provider = Create(handler);

        var result = await provider.CalculateRouteAsync(
            ValidStops());

        Assert.True(result.IsSuccess);
        Assert.Null(result.FailureReason);

        Assert.Equal(
            "GoogleRoutes",
            result.ProviderName);

        Assert.Equal(2.5, result.DistanceKm!.Value, 5);

        Assert.Equal(
            2.5,
            result.EstimatedDurationMinutes!.Value,
            5);

        Assert.Equal(3, result.RouteGeometry.Count);

        Assert.Equal(
            38.5,
            result.RouteGeometry[0].Latitude,
            5);

        Assert.Equal(
            -120.2,
            result.RouteGeometry[0].Longitude,
            5);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Request_ShouldPreserveStopOrderAndHideKey()
    {
        var handler = new FakeHandler();
        var provider = Create(handler);

        await provider.CalculateRouteAsync(ValidStops());

        Assert.Equal(HttpMethod.Post, handler.Method);

        Assert.Equal(
            "https://routes.googleapis.com/directions/v2:computeRoutes",
            handler.Url);

        Assert.Equal(
            "dummy-test-routes-key",
            handler.ApiKey);

        Assert.Contains(
            "routes.polyline.encodedPolyline",
            handler.FieldMask!);

        Assert.DoesNotContain(
            "dummy-test-routes-key",
            handler.Body!);

        Assert.DoesNotContain(
            "dummy-test-routes-key",
            handler.Url!);

        using var json = JsonDocument.Parse(handler.Body!);

        var root = json.RootElement;

        Assert.Equal(
            "DRIVE",
            root.GetProperty("travelMode").GetString());

        Assert.Equal(
            "TRAFFIC_UNAWARE",
            root.GetProperty("routingPreference").GetString());

        var origin = root.GetProperty("origin")
            .GetProperty("location")
            .GetProperty("latLng");

        var destination = root.GetProperty("destination")
            .GetProperty("location")
            .GetProperty("latLng");

        var intermediates =
            root.GetProperty("intermediates");

        Assert.Equal(
            9.660,
            origin.GetProperty("latitude").GetDouble(),
            5);

        Assert.Equal(
            9.670,
            destination.GetProperty("latitude").GetDouble(),
            5);

        var middle = Assert.Single(
            intermediates.EnumerateArray());

        Assert.Equal(
            9.665,
            middle.GetProperty("location")
                .GetProperty("latLng")
                .GetProperty("latitude")
                .GetDouble(),
            5);
    }

    [Fact]
    public async Task DisabledProvider_DoesNotCallGoogle()
    {
        var handler = new FakeHandler();
        var provider = Create(handler, enabled: false);

        var result = await provider.CalculateRouteAsync(
            ValidStops());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.RouteGeometry);
        Assert.Null(result.DistanceKm);
        Assert.Null(result.EstimatedDurationMinutes);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task HttpFailure_DoesNotReturnFakeRoute()
    {
        var handler = new FakeHandler
        {
            Status = HttpStatusCode.ServiceUnavailable
        };

        var provider = Create(handler);

        var result = await provider.CalculateRouteAsync(
            ValidStops());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.RouteGeometry);
        Assert.Null(result.DistanceKm);
        Assert.Null(result.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task MalformedJson_ReturnsFailure()
    {
        var handler = new FakeHandler
        {
            ResponseBody = "not valid json"
        };

        var provider = Create(handler);

        var result = await provider.CalculateRouteAsync(
            ValidStops());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.RouteGeometry);
    }

    [Fact]
    public async Task InvalidDuration_ReturnsFailure()
    {
        var handler = new FakeHandler
        {
            ResponseBody = ValidResponse.Replace(
                "150s",
                "invalid")
        };

        var provider = Create(handler);

        var result = await provider.CalculateRouteAsync(
            ValidStops());

        Assert.False(result.IsSuccess);
        Assert.Null(result.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task InvalidPolyline_ReturnsFailure()
    {
        var handler = new FakeHandler
        {
            ResponseBody = ValidResponse.Replace(
                "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
                "??")
        };

        var provider = Create(handler);

        var result = await provider.CalculateRouteAsync(
            ValidStops());

        Assert.False(result.IsSuccess);
        Assert.Empty(result.RouteGeometry);
    }

    [Fact]
    public async Task InvalidWaypoints_RejectBeforeHttpCall()
    {
        var handler = new FakeHandler();
        var provider = Create(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CalculateRouteAsync(
            [
                new RouteWaypoint(91, 80),
                new RouteWaypoint(9.66, 80.02)
            ]));

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.CalculateRouteAsync(
            [
                new RouteWaypoint(9.66, 80.02)
            ]));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCallProvider()
    {
        var handler = new FakeHandler();
        var provider = Create(handler);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CalculateRouteAsync(
                ValidStops(),
                cancellation.Token));

        Assert.Equal(0, handler.Calls);
    }
}
