
using System;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripMemberDtoTests
{
    private static readonly DateTimeOffset TestTime =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static TripMemberDto CreateMember()
    {
        return TripMemberDto.Create(
            "trip-001",
            "user-001",
            "Accepted",
            "invite-001",
            TestTime,
            Guid.NewGuid());
    }

    [Fact]
    public void Member_Should_Store_Fields()
    {
        var member = CreateMember();

        Assert.Equal("trip-001", member.TripPlanId);
        Assert.Equal("user-001", member.MemberUserId);
        Assert.Equal("Accepted", member.MembershipStatus);
        Assert.Equal("invite-001", member.InvitationReference);
        Assert.Equal(TestTime, member.AcceptedAtUtc);
        Assert.NotNull(member.SessionId);
    }

    [Fact]
    public void Invitation_May_Have_No_Acceptance_Time()
    {
        var member = TripMemberDto.Create(
            "trip-001",
            "user-002",
            "Invited",
            "invite-002",
            null,
            null);

        Assert.Null(member.AcceptedAtUtc);
        Assert.Null(member.SessionId);
    }

    [Fact]
    public void Empty_TripPlanId_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            TripMemberDto.Create(
                " ",
                "user-001",
                "Invited",
                "invite-001",
                null,
                null));
    }

    [Fact]
    public void Empty_UserId_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            TripMemberDto.Create(
                "trip-001",
                "",
                "Invited",
                "invite-001",
                null,
                null));
    }

    [Fact]
    public void Empty_Status_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            TripMemberDto.Create(
                "trip-001",
                "user-001",
                "",
                "invite-001",
                null,
                null));
    }

    [Fact]
    public void Empty_InvitationReference_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            TripMemberDto.Create(
                "trip-001",
                "user-001",
                "Invited",
                "",
                null,
                null));
    }

    [Fact]
    public void Empty_SessionId_Should_Fail()
    {
        Assert.Throws<ArgumentException>(() =>
            TripMemberDto.Create(
                "trip-001",
                "user-001",
                "Accepted",
                "invite-001",
                TestTime,
                Guid.Empty));
    }

    [Fact]
    public void Acceptance_Time_Should_Be_Utc()
    {
        var sriLankaTime = new DateTimeOffset(
            2026, 11, 14, 13, 30, 0,
            TimeSpan.FromMinutes(330));

        var member = TripMemberDto.Create(
            "trip-001",
            "user-001",
            "Accepted",
            "invite-001",
            sriLankaTime,
            null);

        Assert.Equal(TestTime, member.AcceptedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            member.AcceptedAtUtc!.Value.Offset);
    }
}
