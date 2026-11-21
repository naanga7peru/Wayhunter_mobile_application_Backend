using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

public sealed record WeatherForecastPoint(
    DateTimeOffset ForecastAtUtc, double TemperatureCelsius, string Condition);

public interface IWeatherForecastProvider
{
    Task<IReadOnlyList<WeatherForecastPoint>?> GetForecastAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default);
}
