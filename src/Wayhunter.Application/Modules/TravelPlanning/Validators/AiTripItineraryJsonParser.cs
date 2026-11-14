
using System;
using System.Text.Json;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;

namespace Wayhunter.Application.Modules.TravelPlanning.Validators;

public static class AiTripItineraryJsonParser
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            MaxDepth = 32
        };

    public static AiTripItineraryResponse ParseAndValidate(
        AiTripItineraryRequest request,
        string json)
    {
        AiTripItineraryValidator.ValidateRequest(request);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(
                "AI itinerary JSON is required.",
                nameof(json));
        }

        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    MaxDepth = 32,
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });

            // Step 1: Validate required JSON structure.
            ValidateStructure(document.RootElement);

            // Step 2: Convert JSON into typed C# DTOs.
            var response =
                JsonSerializer.Deserialize<AiTripItineraryResponse>(
                    json,
                    JsonOptions);

            if (response is null)
            {
                throw new ArgumentException(
                    "AI itinerary response cannot be null.");
            }

            // Step 3: Validate business rules.
            AiTripItineraryValidator.ValidateResponse(
                request,
                response);

            return response;
        }
        catch (JsonException)
        {
            // Never expose raw AI-generated JSON in errors.
            throw new ArgumentException(
                "Invalid AI itinerary JSON or field types.",
                nameof(json));
        }
    }

    private static void ValidateStructure(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "AI itinerary must be a JSON object.");
        }

        var days = RequireProperty(
            root,
            "days",
            JsonValueKind.Array);

        RequireProperty(
            root,
            "estimatedTotalCostLkr",
            JsonValueKind.Number);

        RequireProperty(
            root,
            "requiresUserReview",
            JsonValueKind.True,
            JsonValueKind.False);

        foreach (var day in days.EnumerateArray())
        {
            RequireProperty(
                day,
                "date",
                JsonValueKind.String);

            var stops = RequireProperty(
                day,
                "stops",
                JsonValueKind.Array);

            foreach (var stop in stops.EnumerateArray())
            {
                RequireProperty(
                    stop,
                    "placeName",
                    JsonValueKind.String);

                RequireProperty(
                    stop,
                    "estimatedCostLkr",
                    JsonValueKind.Number);

                // Optional provider reference.
                ValidateOptionalString(
                    stop,
                    "providerPlaceId");

                // Optional arrival time.
                ValidateOptionalString(
                    stop,
                    "arrivalTime");
            }
        }
    }

    private static JsonElement RequireProperty(
        JsonElement parent,
        string propertyName,
        params JsonValueKind[] validKinds)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(
                propertyName,
                out var value))
        {
            throw new ArgumentException(
                $"Required AI field missing: {propertyName}");
        }

        foreach (var kind in validKinds)
        {
            if (value.ValueKind == kind)
                return value;
        }

        throw new ArgumentException(
            $"Invalid AI field type: {propertyName}");
    }

    private static void ValidateOptionalString(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(
            propertyName,
            out var value))
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.String &&
            value.ValueKind != JsonValueKind.Null)
        {
            throw new ArgumentException(
                $"Invalid AI field type: {propertyName}");
        }
    }
}
