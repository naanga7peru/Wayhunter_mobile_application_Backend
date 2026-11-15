
using System;
using System.Collections.Generic;

namespace Wayhunter.Infrastructure.Integrations.Ai;

public sealed class AiTripProviderOptions
{
    public const string SectionName = "TravelPlanning:AiTrip";

    // Disabled until the server is configured.
    public bool Enabled { get; set; } = false;

    // Server-side secret. Never send to Flutter.
    public string? ApiKey { get; set; }

    // AI model identifier configured by the backend.
    public string? Model { get; set; }

    // Maximum time allowed for an AI request.
    public int TimeoutSeconds { get; set; } = 30;

    // Configuration limit.
    // Enforcement will be implemented separately.
    public int MaxRequestsPerUserPerDay { get; set; } = 10;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Enabled)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                errors.Add(
                    "AI provider server API key is required.");
            }

            if (string.IsNullOrWhiteSpace(Model))
            {
                errors.Add(
                    "AI provider model is required.");
            }
        }

        if (TimeoutSeconds < 5 ||
            TimeoutSeconds > 120)
        {
            errors.Add(
                "AI timeout must be between 5 and 120 seconds.");
        }

        if (MaxRequestsPerUserPerDay < 1 ||
            MaxRequestsPerUserPerDay > 100)
        {
            errors.Add(
                "AI daily request limit must be between 1 and 100.");
        }

        return errors;
    }
}
