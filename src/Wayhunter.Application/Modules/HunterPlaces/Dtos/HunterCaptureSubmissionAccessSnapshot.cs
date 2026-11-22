
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Dtos;

/// <summary>
/// Trusted backend snapshot of a Hunter submission.
/// Used when checking capture session access.
/// Data must come from backend persistence,
/// never directly from a client request.
/// </summary>
public sealed record HunterCaptureSubmissionAccessSnapshot
{
    public Guid SubmissionId { get; init; }

    public string OwnerUserId { get; init; } = string.Empty;

    public HunterSubmissionStatus Status { get; init; }

    public DateTimeOffset? SubmittedAtUtc { get; init; }
}
