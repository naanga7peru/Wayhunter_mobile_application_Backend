
using System;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Domain.Modules.TripSafety.Entities;

public sealed class LiveTripSession
{
    public Guid SessionId { get; }

    public string TripPlanId { get; }

    public LiveTripStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? FinishedAtUtc { get; private set; }

    private LiveTripSession(
        Guid sessionId,
        string tripPlanId,
        DateTimeOffset startedAtUtc)
    {
        SessionId = sessionId;
        TripPlanId = tripPlanId;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        Status = LiveTripStatus.Active;
    }

    public static LiveTripSession Start(
        Guid sessionId,
        string tripPlanId,
        DateTimeOffset serverTimeUtc)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Session ID cannot be empty.",
                nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(tripPlanId))
        {
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (serverTimeUtc == default)
        {
            throw new ArgumentException(
                "Valid server time is required.",
                nameof(serverTimeUtc));
        }

        return new LiveTripSession(
            sessionId,
            tripPlanId.Trim(),
            serverTimeUtc);
    }

    public void Pause()
    {
        if (Status != LiveTripStatus.Active)
        {
            throw new InvalidOperationException(
                "Only an active trip can be paused.");
        }

        Status = LiveTripStatus.Paused;
    }

    public void Resume()
    {
        if (Status != LiveTripStatus.Paused)
        {
            throw new InvalidOperationException(
                "Only a paused trip can be resumed.");
        }

        Status = LiveTripStatus.Active;
    }

    public void Finish(DateTimeOffset serverTimeUtc)
    {
        if (Status == LiveTripStatus.Finished)
        {
            return;
        }

        if (serverTimeUtc == default)
        {
            throw new ArgumentException(
                "Valid server time is required.",
                nameof(serverTimeUtc));
        }

        var finishedTime = serverTimeUtc.ToUniversalTime();

        if (finishedTime < StartedAtUtc)
        {
            throw new InvalidOperationException(
                "Trip cannot finish before it starts.");
        }

        FinishedAtUtc = finishedTime;
        Status = LiveTripStatus.Finished;
    }
}
