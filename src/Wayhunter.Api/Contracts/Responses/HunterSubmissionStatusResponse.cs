using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Api.Contracts.Responses;

/// <summary>
/// Public-safe Hunter submission status projection. Internal ownership,
/// media, location, description, and persistence identifiers are omitted.
/// </summary>
public sealed record HunterSubmissionStatusResponse(
    string PlaceName,
    string District,
    HunterSubmissionStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc);
