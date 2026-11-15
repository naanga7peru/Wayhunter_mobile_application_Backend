
using System;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class LiveTripSessionTests
{
    private static readonly Guid TripPlanId =
        Guid.Parse(
            "11111111-1111-4111-8111-111111111111");

    private static readonly DateTimeOffset StartTime =
        new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

    private static LiveTripSession CreateSession()
    {
        return LiveTripSession.Start(
            Guid.NewGuid(),
            TripPlanId,
            StartTime);
    }

    [Fact]
    public void Start_Should_Create_Active_Session()
    {
        var session = CreateSession();

        Assert.NotEqual(Guid.Empty, session.SessionId);
        Assert.Equal(TripPlanId, session.TripPlanId);
        Assert.Equal(LiveTripStatus.Active, session.Status);
        Assert.Equal(StartTime, session.StartedAtUtc);
        Assert.Null(session.FinishedAtUtc);
    }

    [Fact]
    public void Pause_Should_Change_Status()
    {
        var session = CreateSession();

        session.Pause();

        Assert.Equal(
            LiveTripStatus.Paused,
            session.Status);
    }

    [Fact]
    public void Resume_Should_Change_Status()
    {
        var session = CreateSession();

        session.Pause();
        session.Resume();

        Assert.Equal(
            LiveTripStatus.Active,
            session.Status);
    }

    [Fact]
    public void Finish_Should_Save_Actual_Finish_Time()
    {
        var session = CreateSession();

        var finishTime = StartTime.AddHours(2);

        session.Finish(finishTime);

        Assert.Equal(
            LiveTripStatus.Finished,
            session.Status);

        Assert.Equal(
            finishTime,
            session.FinishedAtUtc);
    }

    [Fact]
    public void Duplicate_Finish_Should_Not_Change_Time()
    {
        var session = CreateSession();

        var firstFinish = StartTime.AddHours(1);

        session.Finish(firstFinish);
        session.Finish(StartTime.AddHours(3));

        Assert.Equal(
            firstFinish,
            session.FinishedAtUtc);
    }

    [Fact]
    public void Resume_Without_Pause_Should_Fail()
    {
        var session = CreateSession();

        Assert.Throws<InvalidOperationException>(
            () => session.Resume());
    }

    [Fact]
    public void Pause_After_Finish_Should_Fail()
    {
        var session = CreateSession();

        session.Finish(StartTime.AddHours(1));

        Assert.Throws<InvalidOperationException>(
            () => session.Pause());
    }

    [Fact]
    public void Finish_Before_Start_Should_Fail()
    {
        var session = CreateSession();

        Assert.Throws<InvalidOperationException>(
            () => session.Finish(
                StartTime.AddMinutes(-1)));
    }

    [Fact]
    public void Empty_TripPlanId_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            LiveTripSession.Start(
                Guid.NewGuid(),
                Guid.Empty,
                StartTime));
    }

    [Fact]
    public void Empty_SessionId_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            LiveTripSession.Start(
                Guid.Empty,
                TripPlanId,
                StartTime));
    }
}
