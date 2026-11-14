
using System;
using System.Collections.Generic;

namespace Wayhunter.Infrastructure.Integrations.Maps;

/// <summary>
/// Server-side configuration for external Maps APIs.
/// Never expose these API keys to mobile clients.
/// </summary>
public sealed class MapsProviderOptions
{
    public const string SectionName = "TravelPlanning:Maps";

    public bool EnableGeocoding { get; set; }

    public bool EnablePlaces { get; set; }

    public bool EnableRoutes { get; set; }

    // Load these from environment variables or
    // .NET User Secrets. Never commit real keys.
    public string? GeocodingServerApiKey { get; set; }

    public string? PlacesServerApiKey { get; set; }

    public string? RoutesServerApiKey { get; set; }

    /// <summary>
    /// Validates configuration without exposing secrets.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (EnableGeocoding &&
            string.IsNullOrWhiteSpace(
                GeocodingServerApiKey))
        {
            errors.Add(
                "Geocoding server API key is required.");
        }

        if (EnablePlaces &&
            string.IsNullOrWhiteSpace(
                PlacesServerApiKey))
        {
            errors.Add(
                "Places server API key is required.");
        }

        if (EnableRoutes &&
            string.IsNullOrWhiteSpace(
                RoutesServerApiKey))
        {
            errors.Add(
                "Routes server API key is required.");
        }

        return errors;
    }
}
