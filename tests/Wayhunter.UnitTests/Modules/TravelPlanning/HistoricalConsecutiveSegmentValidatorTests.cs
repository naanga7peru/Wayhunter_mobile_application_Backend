
using System;
using System.Linq;
using Wayhunter.Application.Modules.TravelPlanning.HistoricalRoutes;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class HistoricalConsecutiveSegmentValidatorTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static (TripPlan Trip, M3VerifiedHistoricalEvidence Evidence)
        BuildValid()
    {
        var trip = new TripPlan(
            "owner-1",
            "Jaffna Tour",
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 16),
            10000m,
            "Car",
            2
        );

        trip.AddStop("Jaffna");
        trip.AddStop("Nallur");
        trip.AddStop("Point Pedro");

        var sessionId = Guid.NewGuid();

        var visits = trip.Stops
            .Select((stop, index) => new M3VerifiedStopVisit(
                trip.Id,
                sessionId,
                stop.Id,
                index,
                Start.AddMinutes(index * 10)
            ))
            .ToArray();

        var evidence = new M3VerifiedHistoricalEvidence(
            trip.Id,
            sessionId,
            "viewer-1",
            true,
            true,
            LiveTripStatus.Finished,
            Start,
            Start.AddHours(1),
            visits
        );

        return (trip, evidence);
    }

    [Fact]
    public void ValidCompletedTrip_ReturnsConsecutiveSegments()
    {
        var (trip, evidence) = BuildValid();

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip, "viewer-1", evidence);

        Assert.Equal(2, result.Count);
        Assert.Equal(trip.Stops[0].Id, result[0].FromStopId);
        Assert.Equal(trip.Stops[1].Id, result[0].ToStopId);
        Assert.Equal(trip.Stops[2].Id, result[1].ToStopId);
    }

    [Fact]
    public void NoReusePermission_ReturnsEmpty()
    {
        var (trip, evidence) = BuildValid();

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip,
            "viewer-1",
            evidence with { ReusePermissionGranted = false });

        Assert.Empty(result);
    }

    [Fact]
    public void WrongViewer_ReturnsEmpty()
    {
        var (trip, evidence) = BuildValid();

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip, "another-user", evidence);

        Assert.Empty(result);
    }

    [Fact]
    public void UnverifiedCompletion_ReturnsEmpty()
    {
        var (trip, evidence) = BuildValid();

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip,
            "viewer-1",
            evidence with { CompletionVerifiedByM3 = false });

        Assert.Empty(result);
    }

    [Fact]
    public void PausedSession_ReturnsEmpty()
    {
        var (trip, evidence) = BuildValid();

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip,
            "viewer-1",
            evidence with
            {
                SessionStatus = LiveTripStatus.Paused
            });

        Assert.Empty(result);
    }

    [Fact]
    public void CancelledTrip_ReturnsEmpty()
    {
        var (trip, evidence) = BuildValid();

        trip.Cancel();

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip, "viewer-1", evidence);

        Assert.Empty(result);
    }

    [Fact]
    public void SkippedStop_IsNotConsideredConsecutive()
    {
        var (trip, evidence) = BuildValid();

        var visits = new[]
        {
            evidence.VerifiedVisits[0],
            evidence.VerifiedVisits[2] with
            {
                VisitSequence = 1
            }
        };

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip,
            "viewer-1",
            evidence with { VerifiedVisits = visits });

        Assert.Empty(result);
    }

    [Fact]
    public void DuplicateVisit_IsRejected()
    {
        var (trip, evidence) = BuildValid();

        var visits = evidence.VerifiedVisits.ToArray();

        visits[1] = visits[1] with
        {
            StopId = visits[0].StopId
        };

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip,
            "viewer-1",
            evidence with { VerifiedVisits = visits });

        Assert.Empty(result);
    }

    [Fact]
    public void DifferentTripVisit_IsRejected()
    {
        var (trip, evidence) = BuildValid();

        var visits = evidence.VerifiedVisits.ToArray();

        visits[1] = visits[1] with
        {
            TripPlanId = Guid.NewGuid()
        };

        var result = HistoricalConsecutiveSegmentValidator.Validate(
            trip,
            "viewer-1",
            evidence with { VerifiedVisits = visits });

        Assert.Empty(result);
    }

    [Fact]
    public void OriginalTrip_RemainsUnchanged()
    {
        var (trip, evidence) = BuildValid();

        var originalIds = trip.Stops
            .Select(stop => stop.Id)
            .ToArray();

        HistoricalConsecutiveSegmentValidator.Validate(
            trip, "viewer-1", evidence);

        Assert.Equal(
            originalIds,
            trip.Stops.Select(stop => stop.Id));
    }
}
