using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Infrastructure.Integrations.Maps;

public sealed class GooglePlacePickerProvider(HttpClient http, IOptions<MapsProviderOptions> options)
    : IPlacesProvider, IPlaceDetailsProvider
{
    private const string Endpoint = "https://places.googleapis.com/v1/places";
    private const string DetailFields = "id,displayName,location";

    public async Task<PlacesProviderResult> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint + ":searchText")
        {
            Content = JsonContent.Create(new { textQuery = query, pageSize = 10 })
        };
        return await SendAsync(request, "places.id,places.displayName,places.location", null, cancellationToken);
    }

    public async Task<PlacesProviderResult> GetDetailsAsync(
        string providerPlaceId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            Endpoint + "/" + Uri.EscapeDataString(providerPlaceId));
        return await SendAsync(request, DetailFields, providerPlaceId, cancellationToken);
    }

    private async Task<PlacesProviderResult> SendAsync(
        HttpRequestMessage request, string fields, string? expectedId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value;
        if (!settings.EnablePlaces || string.IsNullOrWhiteSpace(settings.PlacesServerApiKey))
            return Failure();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        request.Headers.Add("X-Goog-Api-Key", settings.PlacesServerApiKey.Trim());
        request.Headers.Add("X-Goog-FieldMask", fields);

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (expectedId is not null && response.StatusCode == HttpStatusCode.NotFound)
                return Success([]);
            if (!response.IsSuccessStatusCode)
                return Failure();

            await response.Content.LoadIntoBufferAsync(128 * 1024, timeout.Token);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Failure();

            if (expectedId is not null)
            {
                var details = ParsePlace(root);
                return details?.ProviderPlaceId == expectedId ? Success([details]) : Failure();
            }

            if (!root.TryGetProperty("places", out var places))
                return Success([]);
            if (places.ValueKind != JsonValueKind.Array)
                return Failure();

            var results = places.EnumerateArray().Select(ParsePlace).ToArray();
            if (results.Any(place => place is null))
                return Failure();

            return Success(results.OfType<ExternalPlaceSuggestion>()
                .DistinctBy(place => place.ProviderPlaceId).Take(10).ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return Failure();
        }
    }

    private static ExternalPlaceSuggestion? ParsePlace(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String ||
            !item.TryGetProperty("displayName", out var display) || display.ValueKind != JsonValueKind.Object ||
            !display.TryGetProperty("text", out var name) || name.ValueKind != JsonValueKind.String ||
            !item.TryGetProperty("location", out var location) || location.ValueKind != JsonValueKind.Object ||
            !location.TryGetProperty("latitude", out var lat) || lat.ValueKind != JsonValueKind.Number ||
            !location.TryGetProperty("longitude", out var lon) || lon.ValueKind != JsonValueKind.Number ||
            !lat.TryGetDouble(out var latitude) || !lon.TryGetDouble(out var longitude) ||
            !double.IsFinite(latitude) || !double.IsFinite(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return null;

        var placeId = id.GetString();
        var nameText = name.GetString();
        return string.IsNullOrWhiteSpace(placeId) || string.IsNullOrWhiteSpace(nameText)
            ? null
            : new ExternalPlaceSuggestion(placeId, nameText, latitude, longitude, "GooglePlaces");
    }

    private static PlacesProviderResult Success(IReadOnlyList<ExternalPlaceSuggestion> places) =>
        new(true, places, null);

    private static PlacesProviderResult Failure() => new(false, [], "Google Places unavailable.");
}
