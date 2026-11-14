
using System;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripInvitationMembershipTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static TripInvitation CreateInvitation()
    {
        return TripInvitation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-owner",
            "user-friend",
            Now);
    }

    [Fact]
    public void New_Invitation_Should_Be_Pending()
    {
        var invitation = CreateInvitation();

        Assert.Equal(
            TripInvitationStatus.Pending,
            invitation.Status);

        Assert.Null(invitation.RespondedAtUtc);
    }

    [Fact]
    public void Invitation_Should_Store_Relationship()
    {
        var invitation = CreateInvitation();

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.NotEqual(Guid.Empty, invitation.TripPlanId);
        Assert.Equal("user-owner", invitation.SenderUserId);
        Assert.Equal("user-friend", invitation.RecipientUserId);
    }

    [Fact]
    public void Self_Invitation_Should_Fail()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TripInvitation.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "user-001",
                "user-001",
                Now));
    }

    [Fact]
    public void Empty_TripPlanId_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            TripInvitation.Create(
                Guid.NewGuid(),
                Guid.Empty,
                "user-owner",
                "user-friend",
                Now));
    }

    [Fact]
    public void Accept_Should_Change_Status()
    {
        var invitation = CreateInvitation();

        var accepted = invitation.Accept(
            Now.AddMinutes(5));

        Assert.True(accepted);

        Assert.Equal(
            TripInvitationStatus.Accepted,
            invitation.Status);

        Assert.Equal(
            Now.AddMinutes(5),
            invitation.RespondedAtUtc);
    }

    [Fact]
    public void Repeated_Accept_Should_Be_Idempotent()
    {
        var invitation = CreateInvitation();

        invitation.Accept(Now.AddMinutes(5));

        var repeated = invitation.Accept(
            Now.AddMinutes(10));

        Assert.False(repeated);

        Assert.Equal(
            Now.AddMinutes(5),
            invitation.RespondedAtUtc);
    }

    [Fact]
    public void Reject_Should_Change_Status()
    {
        var invitation = CreateInvitation();

        invitation.Reject(Now.AddMinutes(5));

        Assert.Equal(
            TripInvitationStatus.Rejected,
            invitation.Status);
    }

    [Fact]
    public void Cancel_Should_Change_Status()
    {
        var invitation = CreateInvitation();

        invitation.Cancel(Now.AddMinutes(5));

        Assert.Equal(
            TripInvitationStatus.Cancelled,
            invitation.Status);
    }

    [Fact]
    public void Rejected_Invitation_Cannot_Be_Accepted()
    {
        var invitation = CreateInvitation();

        invitation.Reject(Now.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(Now.AddMinutes(10)));
    }

    [Fact]
    public void Response_Before_Creation_Should_Fail()
    {
        var invitation = CreateInvitation();

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(Now.AddMinutes(-1)));
    }

    [Fact]
    public void Pending_Invitation_Cannot_Create_Member()
    {
        var invitation = CreateInvitation();

        Assert.Throws<InvalidOperationException>(() =>
            TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                invitation));
    }

    [Fact]
    public void Accepted_Invitation_Should_Create_Member()
    {
        var invitation = CreateInvitation();

        invitation.Accept(Now.AddMinutes(5));

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        Assert.Equal(
            invitation.TripPlanId,
            member.TripPlanId);

        Assert.Equal(
            invitation.RecipientUserId,
            member.MemberUserId);

        Assert.Equal(
            invitation.Id,
            member.InvitationId);

        Assert.Equal(
            invitation.RespondedAtUtc,
            member.JoinedAtUtc);

        Assert.True(member.IsActive);
    }

    [Fact]
    public void Accepted_Invite_Should_Not_Grant_Gps_Consent()
    {
        var invitation = CreateInvitation();

        invitation.Accept(Now.AddMinutes(5));

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        var gpsConsent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            member.MemberUserId);

        Assert.True(member.IsActive);
        Assert.False(gpsConsent.IsGranted);
    }
}
