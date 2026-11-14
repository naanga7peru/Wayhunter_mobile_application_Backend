
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class ActiveSessionLocationEventValidatorTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static LiveTripSession CreateSession()
    {
        return LiveTripSession.Start(
            Guid.NewGuid(),
            "plan-001",
            StartTime);
    }

    [Fact]
    public void Active_Session_Should_Allow_Event()
    {
        var session = CreateSession();

        var error = Record.Exception(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, "plan-001"));

        Assert.Null(error);
    }

    [Fact]
    public void TripPlanId_With_Spaces_Should_Pass()
    {
        var session = CreateSession();

        var error = Record.Exception(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(
                    session,
                    "  plan-001  "));

        Assert.Null(error);
    }

    [Fact]
    public void Paused_Session_Should_Block_Event()
    {
        var session = CreateSession();
        session.Pause();

        Assert.Throws<InvalidOperationException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, "plan-001"));
    }

    [Fact]
    public void Resumed_Session_Should_Allow_Event()
    {
        var session = CreateSession();

        session.Pause();
        session.Resume();

        var error = Record.Exception(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, "plan-001"));

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
                .EnsureCanPublish(session, "plan-001"));
    }

    [Fact]
    public void Missing_Session_Should_Fail()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(null, "plan-001"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_TripPlanId_Should_Fail(
        string? tripPlanId)
    {
        var session = CreateSession();

        Assert.Throws<ArgumentException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, tripPlanId));
    }

    [Fact]
    public void Wrong_TripPlanId_Should_Be_Rejected()
    {
        var session = CreateSession();

        Assert.Throws<UnauthorizedAccessException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, "plan-002"));
    }

    [Fact]
    public void TripPlanId_Should_Be_Case_Sensitive()
    {
        var session = CreateSession();

        Assert.Throws<UnauthorizedAccessException>(() =>
            ActiveSessionLocationEventValidator
                .EnsureCanPublish(session, "PLAN-001"));
    }

    [Fact]
    public void Validation_Should_Not_Change_Session()
    {
        var session = CreateSession();
        var originalId = session.SessionId;

        ActiveSessionLocationEventValidator
            .EnsureCanPublish(session, "plan-001");

        Assert.Equal(originalId, session.SessionId);
        Assert.Equal(LiveTripStatus.Active, session.Status);
        Assert.Null(session.FinishedAtUtc);
    }
}
