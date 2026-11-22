using System;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

/// <summary>
/// Persisted owner/status projection used by Hunter read services.
/// OwnerUserId must only be populated from trusted storage.
/// </summary>
public sealed record HunterSubmissionStatusSnapshot
{
    public Guid SubmissionId { get; init; }
    public string OwnerUserId { get; init; } = string.Empty;
    public string PlaceName { get; init; } = string.Empty;
    public string District { get; init; } = string.Empty;
    public HunterSubmissionStatus Status { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? SubmittedAtUtc { get; init; }
}
