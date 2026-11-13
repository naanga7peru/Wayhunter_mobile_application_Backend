
using System;

namespace Wayhunter.Domain.Modules.Games.Entities;

public sealed class GameEvidence
{
    public Guid Id { get; }

    public Guid AttemptId { get; }

    public int StepNumber { get; }

    public string MediaId { get; }

    public DateTimeOffset SubmittedAtUtc { get; }

    public GameEvidence(
        Guid id,
        Guid attemptId,
        int stepNumber,
        string mediaId,
        DateTimeOffset submittedAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Evidence ID is required.");

        if (attemptId == Guid.Empty)
            throw new ArgumentException(
                "Attempt ID is required.");

        if (stepNumber <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(stepNumber),
                "Step number must be positive.");

        if (string.IsNullOrWhiteSpace(mediaId))
            throw new ArgumentException(
                "Evidence media reference is required.");

        if (submittedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "UTC submission timestamp is required.");

        Id = id;
        AttemptId = attemptId;
        StepNumber = stepNumber;
        MediaId = mediaId.Trim();
        SubmittedAtUtc = submittedAtUtc;
    }
}
