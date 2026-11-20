
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Infrastructure.Integrations.Weather;

public sealed class OpenWeatherMapProvider : IWeatherProvider, IWeatherForecastProvider
{
    private readonly HttpClient _httpClient;
    private readonly WeatherProviderOptions _options;

    public OpenWeatherMapProvider(
        HttpClient httpClient,
        WeatherProviderOptions options)
    {
        _httpClient = httpClient
            ?? throw new ArgumentNullException(nameof(httpClient));

        _options = options
            ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<WeatherProviderResult> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(latitude, longitude);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
            return Failure("Weather provider is disabled.");

        if (_options.Validate().Count > 0)
            return Failure("Weather provider is not configured.");

        var lat = latitude.ToString(
            "R", CultureInfo.InvariantCulture);

        var lon = longitude.ToString(
            "R", CultureInfo.InvariantCulture);

        var key = Uri.EscapeDataString(_options.ApiKey!);

        var url =
            "https://api.openweathermap.org/data/2.5/weather" +
            $"?lat={lat}&lon={lon}&units=metric&appid={key}";

        using var timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(
            TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            using var request =
                new HttpRequestMessage(HttpMethod.Get, url);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized =>
                        Failure("Weather authentication failed."),

                    HttpStatusCode.TooManyRequests =>
                        Failure("Weather API rate limit exceeded."),

                    _ => Failure(
                        $"Weather provider HTTP {(int)response.StatusCode}.")
                };
            }

            await response.Content.LoadIntoBufferAsync(128 * 1024, timeout.Token);
            await using var stream =
                await response.Content.ReadAsStreamAsync(
                    timeout.Token);

            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: timeout.Token);

            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("main", out var main) ||
                !main.TryGetProperty("temp", out var tempElement) ||
                !tempElement.TryGetDouble(out var temperature) ||
                !double.IsFinite(temperature))
            {
                return Failure("Invalid weather temperature.");
            }

            if (!root.TryGetProperty(
                    "weather", out var weatherArray) ||
                weatherArray.ValueKind != JsonValueKind.Array ||
                weatherArray.GetArrayLength() == 0)
            {
                return Failure("Weather condition is missing.");
            }

            var firstWeather = weatherArray[0];

            if (!firstWeather.TryGetProperty(
                    "main", out var conditionElement) ||
                conditionElement.ValueKind != JsonValueKind.String)
            {
                return Failure("Weather condition is invalid.");
            }

            var condition = conditionElement.GetString();

            if (string.IsNullOrWhiteSpace(condition))
                return Failure("Weather condition is empty.");

            if (!root.TryGetProperty("dt", out var dtElement) ||
                !dtElement.TryGetInt64(out var timestamp) ||
                timestamp <= 0 ||
                timestamp > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            {
                return Failure("Weather observation time is invalid.");
            }

            var observedAt =
                DateTimeOffset.FromUnixTimeSeconds(timestamp);

            if (observedAt > DateTimeOffset.UtcNow)
                return Failure("Weather observation is from the future.");

            var observation = new WeatherObservation(
                latitude,
                longitude,
                temperature,
                condition.Trim(),
                observedAt,
                "OpenWeatherMap");

            return new WeatherProviderResult(
                true,
                observation,
                null);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failure("Weather request timed out.");
        }
        catch (HttpRequestException)
        {
            return Failure("Weather provider unavailable.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return Failure("Invalid weather JSON response.");
        }
    }

    public async Task<IReadOnlyList<WeatherForecastPoint>?> GetForecastAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(latitude, longitude);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled || _options.Validate().Count > 0)
            return null;

        var coordinates = FormattableString.Invariant($"lat={latitude:R}&lon={longitude:R}");
        var key = Uri.EscapeDataString(_options.ApiKey!);
        var url = $"https://api.openweathermap.org/data/2.5/forecast?{coordinates}&units=metric&appid={key}";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            await response.Content.LoadIntoBufferAsync(128 * 1024, timeout.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            var root = document.RootElement;
            if (!root.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array ||
                list.GetArrayLength() is < 1 or > 40)
                return null;

            var forecast = new List<WeatherForecastPoint>(list.GetArrayLength());
            foreach (var point in list.EnumerateArray())
            {
                if (!point.GetProperty("dt").TryGetInt64(out var timestamp) ||
                    timestamp <= 0 || timestamp > DateTimeOffset.MaxValue.ToUnixTimeSeconds() ||
                    !point.GetProperty("main").GetProperty("temp").TryGetDouble(out var temperature) ||
                    !double.IsFinite(temperature))
                    return null;

                var condition = point.GetProperty("weather")[0].GetProperty("main").GetString();
                if (string.IsNullOrWhiteSpace(condition) || condition.Length > 100)
                    return null;

                forecast.Add(new(DateTimeOffset.FromUnixTimeSeconds(timestamp), temperature, condition.Trim()));
            }
            return forecast;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or
                                   InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static WeatherProviderResult Failure(string reason) =>
        new(false, null, reason);

    private static void ValidateCoordinates(
        double latitude,
        double longitude)
    {
        if (!double.IsFinite(latitude) ||
            latitude < -90 || latitude > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude));
        }

        if (!double.IsFinite(longitude) ||
            longitude < -180 || longitude > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude));
        }
    }
}
