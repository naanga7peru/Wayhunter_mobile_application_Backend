
using System;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripInviteEligibilityValidatorTests
{
    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "owner-001",
            "Jaffna Trip",
            new DateOnly(2026, 12, 1),
            new DateOnly(2026, 12, 3),
            5000m,
            "Car",
            3);
    }

    private static void Validate(
        TripPlan trip,
        string sender = "owner-001",
        string recipient = "friend-001",
        bool active = true,
        bool blocked = false,
        TripInvitation? invitation = null,
        TripMember? member = null)
    {
        TripInviteEligibilityValidator.EnsureCanInvite(
            trip,
            sender,
            recipient,
            active,
            blocked,
            invitation,
            member);
    }

    [Fact]
    public void Owner_Can_Invite_Eligible_User()
    {
        Validate(CreateTrip());
    }

    [Fact]
    public void Other_User_Cannot_Invite()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            Validate(CreateTrip(), sender: "other-001"));
    }

    [Fact]
    public void Owner_Cannot_Invite_Self()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Validate(CreateTrip(), recipient: "owner-001"));
    }

    [Fact]
    public void Inactive_User_Cannot_Be_Invited()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Validate(CreateTrip(), active: false));
    }

    [Fact]
    public void Blocked_User_Cannot_Be_Invited()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            Validate(CreateTrip(), blocked: true));
    }

    [Fact]
    public void Cancelled_Trip_Cannot_Invite()
    {
        var trip = CreateTrip();
        trip.Cancel();

        Assert.Throws<InvalidOperationException>(() =>
            Validate(trip));
    }

    [Fact]
    public void Duplicate_Pending_Invitation_Should_Fail()
    {
        var trip = CreateTrip();

        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            trip.Id,
            "owner-001",
            "friend-001",
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            Validate(trip, invitation: invitation));
    }

    [Fact]
    public void Already_Member_Should_Fail()
    {
        var trip = CreateTrip();

        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            trip.Id,
            "owner-001",
            "friend-001",
            DateTimeOffset.UtcNow);

        invitation.Accept(DateTimeOffset.UtcNow);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        Assert.Throws<InvalidOperationException>(() =>
            Validate(trip, member: member));
    }
}
