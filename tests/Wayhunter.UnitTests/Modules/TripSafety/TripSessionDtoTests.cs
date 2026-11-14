
using System;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripSessionDtoTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static LiveTripSession CreateSession()
    {
        return LiveTripSession.Start(
            Guid.NewGuid(),
            "trip-001",
            StartTime);
    }

    [Fact]
    public void ActiveSession_Should_Map_Correctly()
    {
        var session = CreateSession();

        var dto = TripSessionDto.FromDomain(session);

        Assert.Equal(session.SessionId, dto.SessionId);
        Assert.Equal("trip-001", dto.TripPlanId);
        Assert.Equal(LiveTripStatus.Active, dto.Status);
        Assert.Equal(StartTime, dto.StartedAtUtc);
        Assert.Null(dto.FinishedAtUtc);
    }

    [Fact]
    public void PausedSession_Should_Map_Correctly()
    {
        var session = CreateSession();

        session.Pause();

        var dto = TripSessionDto.FromDomain(session);

        Assert.Equal(LiveTripStatus.Paused, dto.Status);
        Assert.Null(dto.FinishedAtUtc);
    }

    [Fact]
    public void FinishedSession_Should_Map_Correctly()
    {
        var session = CreateSession();

        var finishTime = StartTime.AddHours(2);

        session.Finish(finishTime);

        var dto = TripSessionDto.FromDomain(session);

        Assert.Equal(LiveTripStatus.Finished, dto.Status);
        Assert.Equal(StartTime, dto.StartedAtUtc);
        Assert.Equal(finishTime, dto.FinishedAtUtc);
    }

    [Fact]
    public void NullSession_Should_Be_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TripSessionDto.FromDomain(null!));
    }
}
