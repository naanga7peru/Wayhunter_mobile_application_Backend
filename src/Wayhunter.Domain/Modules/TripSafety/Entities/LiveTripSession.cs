
using System;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Domain.Modules.TripSafety.Entities;

public sealed class LiveTripSession
{
    public Guid SessionId { get; }

    // Aligned with M2 TripPlan.Id
    public Guid TripPlanId { get; }

    public LiveTripStatus Status { get; private set; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? FinishedAtUtc { get; private set; }

    private LiveTripSession(
        Guid sessionId,
        Guid tripPlanId,
        DateTimeOffset startedAtUtc)
    {
        SessionId = sessionId;
        TripPlanId = tripPlanId;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        Status = LiveTripStatus.Active;
    }

    public static LiveTripSession Start(
        Guid sessionId,
        Guid tripPlanId,
        DateTimeOffset serverTimeUtc)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Session ID is required.",
                nameof(sessionId));
        }

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
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
            tripPlanId,
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
