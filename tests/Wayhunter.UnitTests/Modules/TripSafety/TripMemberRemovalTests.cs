
using System;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripMemberRemovalTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        JoinedAt.AddHours(1);

    private static TripInvitation CreateAcceptedInvitation()
    {
        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-owner",
            "user-friend",
            CreatedAt);

        invitation.Accept(JoinedAt);

        return invitation;
    }

    private static TripMember CreateActiveMember()
    {
        var invitation = CreateAcceptedInvitation();

        return TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);
    }

    [Fact]
    public void New_Member_Should_Be_Active()
    {
        var member = CreateActiveMember();

        Assert.True(member.IsActive);
        Assert.Null(member.RemovedAtUtc);
    }

    [Fact]
    public void Active_Member_Should_Be_Eligible_For_Private_Access()
    {
        var member = CreateActiveMember();

        Assert.True(member.CanAccessPrivateTrip());
    }

    [Fact]
    public void Remove_Should_Deactivate_Member()
    {
        var member = CreateActiveMember();

        bool result = member.Remove(RemovedAt);

        Assert.True(result);
        Assert.False(member.IsActive);

        Assert.Equal(
            RemovedAt,
            member.RemovedAtUtc);
    }

    [Fact]
    public void Removed_Member_Should_Lose_Private_Access()
    {
        var member = CreateActiveMember();

        member.Remove(RemovedAt);

        Assert.False(member.CanAccessPrivateTrip());
    }

    [Fact]
    public void Repeated_Remove_Should_Be_Idempotent()
    {
        var member = CreateActiveMember();

        bool first = member.Remove(RemovedAt);

        bool second = member.Remove(
            RemovedAt.AddHours(1));

        Assert.True(first);
        Assert.False(second);

        Assert.False(member.IsActive);

        Assert.Equal(
            RemovedAt,
            member.RemovedAtUtc);
    }

    [Fact]
    public void Removal_Before_Joining_Should_Fail()
    {
        var member = CreateActiveMember();

        Assert.Throws<InvalidOperationException>(() =>
            member.Remove(JoinedAt.AddSeconds(-1)));

        Assert.True(member.IsActive);
        Assert.Null(member.RemovedAtUtc);
    }

    [Fact]
    public void Default_Removal_Time_Should_Fail()
    {
        var member = CreateActiveMember();

        Assert.Throws<ArgumentException>(() =>
            member.Remove(default));

        Assert.True(member.IsActive);
    }

    [Fact]
    public void Removal_Should_Store_Utc_Time()
    {
        var member = CreateActiveMember();

        var localTime = RemovedAt.ToOffset(
            TimeSpan.FromHours(5.5));

        member.Remove(localTime);

        Assert.NotNull(member.RemovedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            member.RemovedAtUtc.Value.Offset);

        Assert.Equal(
            RemovedAt,
            member.RemovedAtUtc.Value);
    }

    [Fact]
    public void Removal_Should_Preserve_Member_History()
    {
        var invitation = CreateAcceptedInvitation();

        var memberId = Guid.NewGuid();

        var member = TripMember.FromAcceptedInvitation(
            memberId,
            invitation);

        member.Remove(RemovedAt);

        Assert.Equal(memberId, member.Id);

        Assert.Equal(
            invitation.Id,
            member.InvitationId);

        Assert.Equal(
            invitation.TripPlanId,
            member.TripPlanId);

        Assert.Equal(
            invitation.RecipientUserId,
            member.MemberUserId);

        Assert.Equal(
            JoinedAt,
            member.JoinedAtUtc);

        Assert.False(member.IsActive);
    }

    [Fact]
    public void Removing_Member_Should_Not_Change_Invitation()
    {
        var invitation = CreateAcceptedInvitation();

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        member.Remove(RemovedAt);

        Assert.Equal(
            TripInvitationStatus.Accepted,
            invitation.Status);

        Assert.Equal(
            JoinedAt,
            invitation.RespondedAtUtc);
    }

    [Fact]
    public void Pending_Invitation_Cannot_Create_Member()
    {
        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-owner",
            "user-friend",
            CreatedAt);

        Assert.Throws<InvalidOperationException>(() =>
            TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                invitation));
    }

    [Fact]
    public void Removed_Member_Should_Not_Auto_Reactivate()
    {
        var member = CreateActiveMember();

        member.Remove(RemovedAt);

        member.Remove(RemovedAt.AddDays(1));

        Assert.False(member.IsActive);

        Assert.False(
            member.CanAccessPrivateTrip());
    }
}
