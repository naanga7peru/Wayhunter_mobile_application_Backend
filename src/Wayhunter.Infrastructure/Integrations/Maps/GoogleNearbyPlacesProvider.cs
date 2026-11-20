
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Infrastructure.Integrations.Maps;

public sealed class GoogleNearbyPlacesProvider
    : INearbyPlacesProvider
{
    private const string Endpoint =
        "https://places.googleapis.com/v1/places:searchNearby";

    private const string FieldMask =
        "places.id,places.displayName,places.location";

    private readonly HttpClient _http;
    private readonly IOptions<MapsProviderOptions> _options;

    public GoogleNearbyPlacesProvider(
        HttpClient http,
        IOptions<MapsProviderOptions> options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<PlacesProviderResult> SearchNearbyAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!double.IsFinite(latitude) || latitude < -90 ||
            latitude > 90 || !double.IsFinite(longitude) ||
            longitude < -180 || longitude > 180)
        {
            throw new ArgumentException("Invalid GPS coordinates.");
        }

        if (radiusMeters != 1000)
            throw new ArgumentException("Nearby radius must be 1000 metres.");

        var settings = _options.Value;

        if (!settings.EnablePlaces ||
            string.IsNullOrWhiteSpace(settings.PlacesServerApiKey) ||
            settings.Validate().Count > 0)
        {
            return Failure();
        }

        var placeType = MapGooglePlaceType(searchTerm);

        if (placeType is null)
            return Failure();

        var payload = new
        {
            includedTypes = new[] { placeType },
            maxResultCount = 20,
            rankPreference = "DISTANCE",
            locationRestriction = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = radiusMeters
                }
            }
        };

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(12));

        using var request =
            new HttpRequestMessage(HttpMethod.Post, Endpoint);

        request.Headers.Add(
            "X-Goog-Api-Key",
            settings.PlacesServerApiKey!.Trim());

        request.Headers.TryAddWithoutValidation(
            "X-Goog-FieldMask", FieldMask);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
                return Failure();

            await response.Content.LoadIntoBufferAsync(
                128 * 1024,
                timeout.Token);

            var json = await response.Content.ReadAsStringAsync(
                timeout.Token);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return Failure();

            // Google may omit "places" when no results exist.
            if (!root.TryGetProperty("places", out var places))
            {
                return new PlacesProviderResult(
                    true,
                    Array.Empty<ExternalPlaceSuggestion>(),
                    null);
            }

            if (places.ValueKind != JsonValueKind.Array)
                return Failure();

            var results = new List<ExternalPlaceSuggestion>();

            foreach (var place in places.EnumerateArray())
            {
                if (place.ValueKind != JsonValueKind.Object)
                    continue;

                if (!place.TryGetProperty("id", out var id) ||
                    id.ValueKind != JsonValueKind.String)
                    continue;

                if (!place.TryGetProperty(
                        "displayName", out var display) ||
                    display.ValueKind != JsonValueKind.Object ||
                    !display.TryGetProperty("text", out var name) ||
                    name.ValueKind != JsonValueKind.String)
                    continue;

                if (!place.TryGetProperty(
                        "location", out var location) ||
                    location.ValueKind != JsonValueKind.Object ||
                    !location.TryGetProperty(
                        "latitude", out var latElement) ||
                    !location.TryGetProperty(
                        "longitude", out var lonElement) ||
                    !latElement.TryGetDouble(out var lat) ||
                    !lonElement.TryGetDouble(out var lon))
                    continue;

                var placeId = id.GetString();
                var placeName = name.GetString();

                if (string.IsNullOrWhiteSpace(placeId) ||
                    string.IsNullOrWhiteSpace(placeName) ||
                    !double.IsFinite(lat) ||
                    !double.IsFinite(lon) ||
                    lat is < -90 or > 90 ||
                    lon is < -180 or > 180)
                    continue;

                results.Add(new ExternalPlaceSuggestion(
                    placeId,
                    placeName,
                    lat,
                    lon,
                    "GooglePlaces"));
            }

            return new PlacesProviderResult(
                true,
                results,
                null);
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

    private static string? MapGooglePlaceType(string? term)
    {
        return term?.Trim().ToLowerInvariant() switch
        {
            "restaurant" => "restaurant",
            "hospital" => "hospital",
            "fuel station" => "gas_station",
            "police station" => "police",
            "atm" => "atm",
            "tourist attraction" => "tourist_attraction",
            _ => null
        };
    }

    private static PlacesProviderResult Failure() =>
        new(
            false,
            Array.Empty<ExternalPlaceSuggestion>(),
            "Nearby Places provider unavailable.");
}
