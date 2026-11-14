
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class GroupLiveLocationPublishValidatorTests
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

    private static TripGpsConsent CreateGrantedConsent(
        LiveTripSession session,
        string userId = "user-001")
    {
        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            userId);

        consent.Grant(StartTime);

        return consent;
    }

    [Fact]
    public void Active_Trip_With_Consent_Should_Pass()
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        var error = Record.Exception(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", true));

        Assert.Null(error);
    }

    [Fact]
    public void Paused_Trip_Should_Block_Location()
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        session.Pause();

        Assert.Throws<InvalidOperationException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", true));
    }

    [Fact]
    public void Finished_Trip_Should_Block_Location()
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        session.Finish(StartTime.AddHours(1));

        Assert.Throws<InvalidOperationException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", true));
    }

    [Fact]
    public void Unauthorized_Participant_Should_Fail()
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", false));
    }

    [Fact]
    public void Disabled_Gps_Consent_Should_Fail()
    {
        var session = CreateSession();

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-001");

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", true));
    }

    [Fact]
    public void Revoked_Gps_Consent_Should_Fail()
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        consent.Revoke(StartTime.AddMinutes(5));

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", true));
    }

    [Fact]
    public void Another_Session_Consent_Should_Fail()
    {
        var session = CreateSession();

        var consent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            "user-001");

        consent.Grant(StartTime);

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", true));
    }

    [Fact]
    public void Another_User_Consent_Should_Fail()
    {
        var session = CreateSession();

        var consent = CreateGrantedConsent(
            session,
            "user-002");

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", true));
    }

    [Fact]
    public void Missing_Consent_Should_Fail()
    {
        var session = CreateSession();

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, null, "plan-001", "user-001", true));
    }

    [Fact]
    public void Missing_Session_Should_Fail()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                null, null, "plan-001", "user-001", true));
    }

    [Fact]
    public void Wrong_TripPlan_Should_Fail()
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-002", "user-001", true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_Authentication_Should_Fail(
        string? userId)
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", userId, true));
    }

    [Fact]
    public void Trimmed_UserId_Should_Pass()
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        var error = Record.Exception(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                "plan-001",
                "  user-001  ",
                true));

        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_TripPlanId_Should_Fail(
        string? tripPlanId)
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        Assert.Throws<ArgumentException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, tripPlanId, "user-001", true));
    }

    [Fact]
    public void ReGranted_Gps_Consent_Should_Pass()
    {
        var session = CreateSession();
        var consent = CreateGrantedConsent(session);

        consent.Revoke(StartTime.AddMinutes(5));
        consent.Grant(StartTime.AddMinutes(10));

        var error = Record.Exception(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session, consent, "plan-001", "user-001", true));

        Assert.Null(error);
        Assert.True(consent.IsGranted);
        Assert.Null(consent.RevokedAtUtc);
    }
}
