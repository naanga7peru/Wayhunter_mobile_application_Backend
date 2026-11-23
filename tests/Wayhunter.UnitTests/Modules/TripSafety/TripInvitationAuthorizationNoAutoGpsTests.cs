
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripInvitationAuthorizationNoAutoGpsTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DecisionAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset SessionTime =
        DecisionAt.AddMinutes(1);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return DecisionAt;
        }
    }

    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "user-owner",
            "Jaffna Group Trip",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Van",
            4);
    }

    private static TripInvitation CreateInvitation(Guid tripPlanId)
    {
        return TripInvitation.Create(
            Guid.NewGuid(),
            tripPlanId,
            "user-owner",
            "user-friend",
            CreatedAt);
    }

    private static LiveTripSession CreateSession(Guid tripPlanId)
    {
        return LiveTripSession.Start(
            Guid.NewGuid(),
            tripPlanId,
            SessionTime);
    }

    private static async Task<(
        TripPlan Trip,
        TripInvitation Invitation,
        InMemoryTripInvitationDecisionRepository Repository,
        TripInvitationDecisionService Service)> SetupAsync()
    {
        var trip = CreateTrip();
        var invitation = CreateInvitation(trip.Id);

        var repository =
            new InMemoryTripInvitationDecisionRepository();

        await repository.AddAsync(invitation);

        var service = new TripInvitationDecisionService(
            repository,
            new FixedClock(),
            new TripInviteBlockReaderStub());

        return (trip, invitation, repository, service);
    }

    [Fact]
    public void Eligible_Owner_Can_Invite_Active_User()
    {
        var trip = CreateTrip();

        TripInviteEligibilityValidator.EnsureCanInvite(
            trip,
            "user-owner",
            "user-friend",
            recipientIsActive: true,
            isBlockedRelationship: false,
            existingInvitation: null,
            existingMember: null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("user-stranger")]
    public void Unauthenticated_Or_NonOwner_Cannot_Invite(
        string? senderUserId)
    {
        var trip = CreateTrip();

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripInviteEligibilityValidator.EnsureCanInvite(
                trip,
                senderUserId,
                "user-friend",
                true,
                false,
                null,
                null));
    }

    [Fact]
    public void Blocked_Relationship_Cannot_Invite()
    {
        var trip = CreateTrip();

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripInviteEligibilityValidator.EnsureCanInvite(
                trip,
                "user-owner",
                "user-friend",
                true,
                true,
                null,
                null));
    }

    [Fact]
    public void Inactive_Recipient_Cannot_Be_Invited()
    {
        var trip = CreateTrip();

        Assert.Throws<InvalidOperationException>(() =>
            TripInviteEligibilityValidator.EnsureCanInvite(
                trip,
                "user-owner",
                "user-friend",
                false,
                false,
                null,
                null));
    }

    [Fact]
    public void Owner_Cannot_Invite_Themselves()
    {
        var trip = CreateTrip();

        Assert.Throws<InvalidOperationException>(() =>
            TripInviteEligibilityValidator.EnsureCanInvite(
                trip,
                "user-owner",
                "user-owner",
                true,
                false,
                null,
                null));
    }

    [Fact]
    public void Duplicate_Pending_Invitation_Is_Rejected()
    {
        var trip = CreateTrip();
        var existing = CreateInvitation(trip.Id);

        Assert.Throws<InvalidOperationException>(() =>
            TripInviteEligibilityValidator.EnsureCanInvite(
                trip,
                "user-owner",
                "user-friend",
                true,
                false,
                existing,
                null));
    }

    [Fact]
    public void Existing_Active_Member_Cannot_Be_Reinvited()
    {
        var trip = CreateTrip();
        var invitation = CreateInvitation(trip.Id);

        invitation.Accept(DecisionAt);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        Assert.Throws<InvalidOperationException>(() =>
            TripInviteEligibilityValidator.EnsureCanInvite(
                trip,
                "user-owner",
                "user-friend",
                true,
                false,
                null,
                member));
    }

    [Fact]
    public void Cancelled_Trip_Cannot_Send_Invitation()
    {
        var trip = CreateTrip();

        trip.Cancel();

        Assert.Throws<InvalidOperationException>(() =>
            TripInviteEligibilityValidator.EnsureCanInvite(
                trip,
                "user-owner",
                "user-friend",
                true,
                false,
                null,
                null));
    }

    [Theory]
    [InlineData("user-owner")]
    [InlineData("user-stranger")]
    public async Task Only_Recipient_Can_Accept(
        string invalidUserId)
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Service.AcceptAsync(
                f.Invitation.Id,
                invalidUserId));

        var stored = await f.Repository.GetByIdAsync(
            f.Invitation.Id);

        Assert.NotNull(stored);
        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);

        Assert.Null(
            await f.Repository.GetMemberByInvitationIdAsync(
                f.Invitation.Id));
    }

    [Fact]
    public async Task Only_Recipient_Can_Reject()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Service.RejectAsync(
                f.Invitation.Id,
                "user-owner"));

        var stored = await f.Repository.GetByIdAsync(
            f.Invitation.Id);

        Assert.NotNull(stored);
        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
    }

    [Fact]
    public async Task Only_Sender_Can_Cancel()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Service.CancelAsync(
                f.Invitation.Id,
                "user-friend"));

        var stored = await f.Repository.GetByIdAsync(
            f.Invitation.Id);

        Assert.NotNull(stored);
        Assert.Equal(
            TripInvitationStatus.Pending,
            stored.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_Authentication_Cannot_Accept(
        string? userId)
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Service.AcceptAsync(
                f.Invitation.Id,
                userId));
    }

    [Fact]
    public async Task Accepted_Invitation_Creates_One_Member()
    {
        var f = await SetupAsync();

        var member = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        var storedMember =
            await f.Repository.GetMemberByInvitationIdAsync(
                f.Invitation.Id);

        Assert.NotNull(storedMember);
        Assert.Equal(member.Id, storedMember.Id);
        Assert.Equal(f.Trip.Id, storedMember.TripPlanId);
        Assert.Equal("user-friend", storedMember.MemberUserId);
        Assert.True(storedMember.IsActive);
    }

    [Fact]
    public async Task Accept_Does_Not_Automatically_Enable_Gps()
    {
        var f = await SetupAsync();
        var session = CreateSession(f.Trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-friend");

        var member = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        Assert.True(member.IsActive);
        Assert.False(consent.IsGranted);
        Assert.Null(consent.GrantedAtUtc);

        // Accepted membership alone cannot publish GPS.
        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                f.Trip.Id,
                "user-friend",
                member.IsActive));
    }

    [Fact]
    public async Task Repeated_Accept_Does_Not_Enable_Gps()
    {
        var f = await SetupAsync();
        var session = CreateSession(f.Trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-friend");

        var first = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        var second = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        Assert.Equal(first.Id, second.Id);
        Assert.False(consent.IsGranted);
        Assert.Null(consent.GrantedAtUtc);

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                f.Trip.Id,
                "user-friend",
                second.IsActive));
    }

    [Fact]
    public void Gps_Consent_Without_Membership_Is_Not_Enough()
    {
        var trip = CreateTrip();
        var session = CreateSession(trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-stranger");

        consent.Grant(SessionTime.AddMinutes(1));

        // Even explicit consent cannot override
        // a failed membership authorization check.
        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                trip.Id,
                "user-stranger",
                isAuthorizedTripParticipant: false));
    }

    [Fact]
    public async Task Explicit_Consent_Allows_Eligible_Member_Gps()
    {
        var f = await SetupAsync();

        var member = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        var session = CreateSession(f.Trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-friend");

        Assert.False(consent.IsGranted);

        // Simulate a separate explicit consent action.
        // This is NOT called by invitation acceptance.
        consent.Grant(SessionTime.AddMinutes(1));

        GroupLiveLocationPublishValidator.EnsureCanPublish(
            session,
            consent,
            f.Trip.Id,
            "user-friend",
            member.IsActive);

        Assert.True(consent.IsGranted);
    }

    [Fact]
    public async Task Revoked_Consent_Blocks_Accepted_Member_Gps()
    {
        var f = await SetupAsync();

        var member = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        var session = CreateSession(f.Trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-friend");

        consent.Grant(SessionTime.AddMinutes(1));
        consent.Revoke(SessionTime.AddMinutes(2));

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                f.Trip.Id,
                "user-friend",
                member.IsActive));

        Assert.False(consent.IsGranted);
    }

    [Fact]
    public async Task Another_Session_Consent_Cannot_Publish()
    {
        var f = await SetupAsync();

        var member = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        var session = CreateSession(f.Trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            "user-friend");

        consent.Grant(SessionTime.AddMinutes(1));

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                f.Trip.Id,
                "user-friend",
                member.IsActive));
    }

    [Fact]
    public async Task Another_Users_Consent_Cannot_Publish()
    {
        var f = await SetupAsync();

        var member = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        var session = CreateSession(f.Trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-stranger");

        consent.Grant(SessionTime.AddMinutes(1));

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                f.Trip.Id,
                "user-friend",
                member.IsActive));
    }

    [Fact]
    public async Task Different_Trip_Cannot_Receive_Gps()
    {
        var f = await SetupAsync();

        var member = await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        var session = CreateSession(f.Trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-friend");

        consent.Grant(SessionTime.AddMinutes(1));

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                Guid.NewGuid(),
                "user-friend",
                member.IsActive));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rejected_Or_Cancelled_Invite_Creates_No_Member(
        bool cancel)
    {
        var f = await SetupAsync();

        if (cancel)
        {
            await f.Service.CancelAsync(
                f.Invitation.Id,
                "user-owner");
        }
        else
        {
            await f.Service.RejectAsync(
                f.Invitation.Id,
                "user-friend");
        }

        var member =
            await f.Repository.GetMemberByInvitationIdAsync(
                f.Invitation.Id);

        Assert.Null(member);

        var session = CreateSession(f.Trip.Id);

        var consent = TripGpsConsent.CreateDisabled(
            session.SessionId,
            "user-friend");

        Assert.False(consent.IsGranted);

        Assert.Throws<UnauthorizedAccessException>(() =>
            GroupLiveLocationPublishValidator.EnsureCanPublish(
                session,
                consent,
                f.Trip.Id,
                "user-friend",
                isAuthorizedTripParticipant: false));
    }

    [Fact]
    public async Task Accepted_Invite_Cannot_Be_Cancelled()
    {
        var f = await SetupAsync();

        await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.CancelAsync(
                f.Invitation.Id,
                "user-owner"));
    }

    [Fact]
    public async Task Accepted_Invite_Cannot_Be_Rejected()
    {
        var f = await SetupAsync();

        await f.Service.AcceptAsync(
            f.Invitation.Id,
            "user-friend");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.RejectAsync(
                f.Invitation.Id,
                "user-friend"));
    }
}
