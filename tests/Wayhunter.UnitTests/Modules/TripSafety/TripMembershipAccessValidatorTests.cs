
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripMembershipAccessValidatorTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        JoinedAt.AddHours(1);

    private static TripPlan CreateTrip(
        string ownerUserId = "user-owner")
    {
        return new TripPlan(
            ownerUserId,
            "Jaffna Group Trip",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Van",
            4);
    }

    private static TripMember CreateActiveMember(
        Guid tripPlanId,
        string memberUserId = "user-friend")
    {
        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            tripPlanId,
            "user-owner",
            memberUserId,
            CreatedAt);

        invitation.Accept(JoinedAt);

        return TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);
    }

    [Fact]
    public void Trip_Owner_Should_Have_Access()
    {
        var trip = CreateTrip();

        TripMembershipAccessValidator.EnsureCanAccess(
            trip,
            null,
            "user-owner");
    }

    [Fact]
    public void Owner_Should_Not_Require_Membership()
    {
        var trip = CreateTrip();

        var anotherMember = CreateActiveMember(
            trip.Id,
            "user-friend");

        TripMembershipAccessValidator.EnsureCanAccess(
            trip,
            anotherMember,
            "user-owner");
    }

    [Fact]
    public void Active_Member_Should_Have_Access()
    {
        var trip = CreateTrip();

        var member = CreateActiveMember(
            trip.Id);

        TripMembershipAccessValidator.EnsureCanAccess(
            trip,
            member,
            "user-friend");

        Assert.True(member.IsActive);
    }

    [Fact]
    public void Removed_Member_Should_Be_Denied()
    {
        var trip = CreateTrip();

        var member = CreateActiveMember(
            trip.Id);

        member.Remove(RemovedAt);

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                trip,
                member,
                "user-friend"));

        Assert.False(member.IsActive);
    }

    [Fact]
    public void Missing_Membership_Should_Be_Denied()
    {
        var trip = CreateTrip();

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                trip,
                null,
                "user-friend"));
    }

    [Fact]
    public void Member_Of_Another_Trip_Should_Be_Denied()
    {
        var requestedTrip = CreateTrip();

        var anotherTrip = CreateTrip();

        var member = CreateActiveMember(
            anotherTrip.Id);

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                requestedTrip,
                member,
                "user-friend"));
    }

    [Fact]
    public void Another_Users_Membership_Should_Be_Denied()
    {
        var trip = CreateTrip();

        var member = CreateActiveMember(
            trip.Id,
            "user-friend");

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                trip,
                member,
                "user-other"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unauthenticated_User_Should_Be_Denied(
        string? userId)
    {
        var trip = CreateTrip();

        var member = CreateActiveMember(
            trip.Id);

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                trip,
                member,
                userId));
    }

    [Fact]
    public void Missing_Trip_Should_Fail()
    {
        var member = CreateActiveMember(
            Guid.NewGuid());

        Assert.Throws<KeyNotFoundException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                null,
                member,
                "user-friend"));
    }

    [Fact]
    public void Unknown_User_Should_Be_Denied()
    {
        var trip = CreateTrip();

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                trip,
                null,
                "unknown-user"));
    }

    [Fact]
    public void Authenticated_UserId_Should_Be_Trimmed()
    {
        var trip = CreateTrip();

        var member = CreateActiveMember(
            trip.Id);

        TripMembershipAccessValidator.EnsureCanAccess(
            trip,
            member,
            "  user-friend  ");
    }

    [Fact]
    public void Owner_UserId_Should_Be_Trimmed()
    {
        var trip = CreateTrip();

        TripMembershipAccessValidator.EnsureCanAccess(
            trip,
            null,
            "  user-owner  ");
    }

    [Fact]
    public void UserId_Comparison_Should_Be_Case_Sensitive()
    {
        var trip = CreateTrip();

        var member = CreateActiveMember(
            trip.Id,
            "user-friend");

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                trip,
                member,
                "USER-FRIEND"));
    }

    [Fact]
    public void Repeated_Removal_Should_Not_Restore_Access()
    {
        var trip = CreateTrip();

        var member = CreateActiveMember(
            trip.Id);

        member.Remove(RemovedAt);

        member.Remove(RemovedAt.AddHours(2));

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                trip,
                member,
                "user-friend"));

        Assert.False(member.IsActive);

        Assert.Equal(
            RemovedAt,
            member.RemovedAtUtc);
    }

    [Fact]
    public void Removing_Member_Should_Not_Revoke_Owner_Access()
    {
        var trip = CreateTrip();

        var member = CreateActiveMember(
            trip.Id);

        member.Remove(RemovedAt);

        TripMembershipAccessValidator.EnsureCanAccess(
            trip,
            null,
            "user-owner");

        Assert.False(member.IsActive);
    }
}
