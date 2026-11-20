
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Infrastructure.Integrations.Maps;

public sealed class GoogleRoutesProvider : IRoutesProvider
{
    private const string Endpoint =
        "https://routes.googleapis.com/directions/v2:computeRoutes";

    private const string FieldMask =
        "routes.distanceMeters,routes.duration," +
        "routes.polyline.encodedPolyline";

    private readonly HttpClient _http;
    private readonly IOptions<MapsProviderOptions> _options;

    public GoogleRoutesProvider(
        HttpClient http,
        IOptions<MapsProviderOptions> options)
    {
        _http = http ??
            throw new ArgumentNullException(nameof(http));

        _options = options ??
            throw new ArgumentNullException(nameof(options));
    }

    public async Task<RouteProviderResult> CalculateRouteAsync(
        IReadOnlyList<RouteWaypoint> orderedStops,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedStops);
        cancellationToken.ThrowIfCancellationRequested();

        // Origin + destination + up to 25 intermediate stops.
        if (orderedStops.Count < 2 || orderedStops.Count > 27)
        {
            throw new ArgumentException(
                "A route requires 2 to 27 ordered waypoints.",
                nameof(orderedStops));
        }

        foreach (var stop in orderedStops)
        {
            if (stop is null ||
                !ValidCoordinates(stop.Latitude, stop.Longitude))
            {
                throw new ArgumentException(
                    "Invalid route waypoint coordinates.",
                    nameof(orderedStops));
            }
        }

        var settings = _options.Value;

        // Fail closed when Routes is disabled or unconfigured.
        if (!settings.EnableRoutes ||
            string.IsNullOrWhiteSpace(settings.RoutesServerApiKey) ||
            settings.Validate().Count > 0)
        {
            return Failure();
        }

        // Preserve the exact input waypoint order.
        var points = orderedStops.Select(stop => new
        {
            location = new
            {
                latLng = new
                {
                    latitude = stop.Latitude,
                    longitude = stop.Longitude
                }
            }
        }).ToArray();

        var payload = new
        {
            origin = points[0],
            destination = points[^1],
            intermediates = points
                .Skip(1)
                .Take(points.Length - 2)
                .ToArray(),
            travelMode = "DRIVE",
            routingPreference = "TRAFFIC_UNAWARE",
            polylineQuality = "HIGH_QUALITY",
            computeAlternativeRoutes = false
        };

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                Endpoint);

            request.Headers.Add(
                "X-Goog-Api-Key",
                settings.RoutesServerApiKey!.Trim());

            request.Headers.TryAddWithoutValidation(
                "X-Goog-FieldMask",
                FieldMask);

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
                return Failure();

            // Reject oversized external responses.
            await response.Content.LoadIntoBufferAsync(
                512 * 1024,
                timeout.Token);

            await using var stream =
                await response.Content.ReadAsStreamAsync(
                    timeout.Token);

            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: timeout.Token);

            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("routes", out var routes) ||
                routes.ValueKind != JsonValueKind.Array ||
                routes.GetArrayLength() == 0)
            {
                return Failure();
            }

            var route = routes[0];

            if (route.ValueKind != JsonValueKind.Object)
                return Failure();

            if (!route.TryGetProperty(
                    "distanceMeters", out var distanceElement) ||
                !distanceElement.TryGetDouble(out var distance) ||
                !double.IsFinite(distance) ||
                distance < 0)
            {
                return Failure();
            }

            if (!route.TryGetProperty(
                    "duration", out var durationElement) ||
                durationElement.ValueKind != JsonValueKind.String)
            {
                return Failure();
            }

            var durationText = durationElement.GetString();

            if (string.IsNullOrWhiteSpace(durationText) ||
                !durationText.EndsWith(
                    "s", StringComparison.Ordinal))
            {
                return Failure();
            }

            if (!double.TryParse(
                    durationText[..^1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var durationSeconds) ||
                !double.IsFinite(durationSeconds) ||
                durationSeconds < 0)
            {
                return Failure();
            }

            if (!route.TryGetProperty(
                    "polyline", out var polyline) ||
                polyline.ValueKind != JsonValueKind.Object ||
                !polyline.TryGetProperty(
                    "encodedPolyline", out var encodedElement) ||
                encodedElement.ValueKind != JsonValueKind.String)
            {
                return Failure();
            }

            var encoded = encodedElement.GetString();

            if (string.IsNullOrWhiteSpace(encoded) ||
                !TryDecodePolyline(encoded, out var geometry))
            {
                return Failure();
            }

            return new RouteProviderResult(
                true,
                geometry,
                distance / 1000.0,
                durationSeconds / 60.0,
                "GoogleRoutes",
                null)
            {
                EncodedPolyline = encoded
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failure();
        }
        catch (HttpRequestException)
        {
            return Failure();
        }
        catch (JsonException)
        {
            return Failure();
        }
        catch (InvalidOperationException)
        {
            return Failure();
        }
    }

    private static bool ValidCoordinates(
        double latitude,
        double longitude)
    {
        return double.IsFinite(latitude) &&
               double.IsFinite(longitude) &&
               latitude >= -90 &&
               latitude <= 90 &&
               longitude >= -180 &&
               longitude <= 180;
    }

    private static bool TryDecodePolyline(
        string encoded,
        out IReadOnlyList<RouteWaypoint> geometry)
    {
        var points = new List<RouteWaypoint>();

        var index = 0;
        long latitude = 0;
        long longitude = 0;

        geometry = Array.Empty<RouteWaypoint>();

        while (index < encoded.Length)
        {
            if (points.Count >= 20000 ||
                !TryReadChunk(encoded, ref index, out var latDelta) ||
                !TryReadChunk(encoded, ref index, out var lonDelta))
            {
                return false;
            }

            latitude += latDelta;
            longitude += lonDelta;

            var lat = latitude / 100000.0;
            var lon = longitude / 100000.0;

            if (!ValidCoordinates(lat, lon))
                return false;

            points.Add(new RouteWaypoint(lat, lon));
        }

        if (points.Count < 2)
            return false;

        geometry = points;
        return true;
    }

    private static bool TryReadChunk(
        string encoded,
        ref int index,
        out int value)
    {
        value = 0;

        long result = 0;
        var shift = 0;

        while (index < encoded.Length && shift <= 30)
        {
            var part = encoded[index++] - 63;

            if (part < 0 || part > 63)
                return false;

            result |= ((long)(part & 31)) << shift;

            if ((part & 32) == 0)
            {
                var signed = (result & 1) == 1
                    ? ~(result >> 1)
                    : result >> 1;

                if (signed < int.MinValue ||
                    signed > int.MaxValue)
                    return false;

                value = (int)signed;
                return true;
            }

            shift += 5;
        }

        return false;
    }

    private static RouteProviderResult Failure()
    {
        // No fabricated geometry, distance or ETA.
        return new RouteProviderResult(
            false,
            Array.Empty<RouteWaypoint>(),
            null,
            null,
            null,
            "Routes provider unavailable.");
    }
}
