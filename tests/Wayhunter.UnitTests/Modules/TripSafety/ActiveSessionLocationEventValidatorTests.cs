
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class ActiveSessionLocationEventValidatorTests
{
    private static readonly Guid TripPlanId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static readonly Guid AnotherTripPlanId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static readonly DateTimeOffset StartTime =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static LiveTripSession CreateSession()
    {
        return LiveTripSession.Start(
            Guid.NewGuid(),
            TripPlanId,
            StartTime);
    }

    [Fact]
    public void Active_Session_Should_Allow_Event()
    {
        var session = CreateSession();

        var error = Record.Exception(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, TripPlanId));

        Assert.Null(error);
    }

    [Fact]
    public void Same_TripPlanId_Should_Pass()
    {
        var session = CreateSession();

        var error = Record.Exception(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(
                    session,
                    session.TripPlanId));

        Assert.Null(error);
    }

    [Fact]
    public void Paused_Session_Should_Block_Event()
    {
        var session = CreateSession();
        session.Pause();

        Assert.Throws<InvalidOperationException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, TripPlanId));
    }

    [Fact]
    public void Resumed_Session_Should_Allow_Event()
    {
        var session = CreateSession();

        session.Pause();
        session.Resume();

        var error = Record.Exception(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, TripPlanId));

        Assert.Null(error);
        Assert.Equal(LiveTripStatus.Active, session.Status);
    }

    [Fact]
    public void Finished_Session_Should_Block_Event()
    {
        var session = CreateSession();

        session.Finish(StartTime.AddHours(2));

        Assert.Throws<InvalidOperationException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, TripPlanId));
    }

    [Fact]
    public void Missing_Session_Should_Fail()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(null, TripPlanId));
    }

    [Fact]
    public void Empty_TripPlanId_Should_Fail()
    {
        var session = CreateSession();

        Assert.Throws<ArgumentException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, Guid.Empty));
    }

    [Fact]
    public void Wrong_TripPlanId_Should_Be_Rejected()
    {
        var session = CreateSession();

        Assert.Throws<UnauthorizedAccessException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(
                    session,
                    AnotherTripPlanId));
    }

    [Fact]
    public void Validation_Should_Not_Change_Session()
    {
        var session = CreateSession();
        var originalId = session.SessionId;

        ActiveSessionLocationEventValidator
            .EnsureCanPublish(session, TripPlanId);

        Assert.Equal(originalId, session.SessionId);
        Assert.Equal(TripPlanId, session.TripPlanId);
        Assert.Equal(LiveTripStatus.Active, session.Status);
        Assert.Null(session.FinishedAtUtc);
    }
}
