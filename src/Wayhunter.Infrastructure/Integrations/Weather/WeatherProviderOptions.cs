
using System;
using System.Collections.Generic;

namespace Wayhunter.Infrastructure.Integrations.Weather;

public sealed class WeatherProviderOptions
{
    public const string SectionName =
        "TravelPlanning:Weather";

    // Disabled until an external provider is configured.
    public bool Enabled { get; set; } = false;

    // Server-side secret. Never expose to Flutter.
    public string? ApiKey { get; set; }

    // Cache freshness configuration.
    public int CacheMaxAgeMinutes { get; set; } = 30;

    // External provider request timeout.
    public int TimeoutSeconds { get; set; } = 15;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Enabled &&
            string.IsNullOrWhiteSpace(ApiKey))
        {
            errors.Add(
                "Weather provider API key is required.");
        }

        if (CacheMaxAgeMinutes < 1 ||
            CacheMaxAgeMinutes > 1440)
        {
            errors.Add(
                "Weather cache maximum age must be " +
                "between 1 and 1440 minutes.");
        }

        if (TimeoutSeconds < 5 ||
            TimeoutSeconds > 60)
        {
            errors.Add(
                "Weather timeout must be " +
                "between 5 and 60 seconds.");
        }

        return errors;
    }

    public TimeSpan GetCacheMaximumAge()
    {
        if (CacheMaxAgeMinutes < 1 ||
            CacheMaxAgeMinutes > 1440)
        {
            throw new InvalidOperationException(
                "Invalid weather cache maximum age.");
        }

        return TimeSpan.FromMinutes(
            CacheMaxAgeMinutes);
    }
}
