
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

// Provider-neutral weather contract.
// External API integration will be added later.
public interface IWeatherProvider
{
    Task<WeatherProviderResult> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}

// Actual weather data returned by a provider.
public sealed record WeatherObservation(
    double Latitude,
    double Longitude,
    double TemperatureCelsius,
    string Condition,
    DateTimeOffset ObservedAtUtc,
    string ProviderName
);

// Explicit success or failure response.
public sealed record WeatherProviderResult(
    bool IsSuccess,
    WeatherObservation? Observation,
    string? FailureReason
);
