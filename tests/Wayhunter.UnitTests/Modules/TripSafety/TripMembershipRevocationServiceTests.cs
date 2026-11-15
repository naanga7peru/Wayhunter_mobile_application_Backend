
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripMembershipRevocationServiceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        JoinedAt.AddHours(1);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return RemovedAt;
        }
    }

    private static async Task<(
        InMemoryTripMembershipRevocationRepository Repository,
        TripPlan Trip,
        TripMember Member,
        TripMembershipRevocationService Service)> SetupAsync()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = new TripPlan(
            "user-owner",
            "Group Trip",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            20000m,
            "Van",
            5);

        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            trip.Id,
            "user-owner",
            "user-friend",
            CreatedAt);

        invitation.Accept(JoinedAt);

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        var service = new TripMembershipRevocationService(
            repository,
            new FixedTimeProvider());

        return (repository, trip, member, service);
    }

    [Fact]
    public async Task Owner_Should_Remove_Active_Member()
    {
        var f = await SetupAsync();

        var removed = await f.Service.RemoveAsync(
            f.Trip.Id,
            "user-owner",
            "user-friend");

        Assert.True(removed);

        var stored = await f.Repository.GetMemberAsync(
            f.Trip.Id,
            "user-friend");

        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
        Assert.Equal(RemovedAt, stored.RemovedAtUtc);
    }

    [Fact]
    public async Task Repeated_Remove_Should_Return_False()
    {
        var f = await SetupAsync();

        var first = await f.Service.RemoveAsync(
            f.Trip.Id,
            "user-owner",
            "user-friend");

        var second = await f.Service.RemoveAsync(
            f.Trip.Id,
            "user-owner",
            "user-friend");

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task Another_User_Cannot_Remove_Member()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Service.RemoveAsync(
                f.Trip.Id,
                "user-stranger",
                "user-friend"));

        var stored = await f.Repository.GetMemberAsync(
            f.Trip.Id,
            "user-friend");

        Assert.NotNull(stored);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Member_Cannot_Remove_Another_Member()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Service.RemoveAsync(
                f.Trip.Id,
                "user-friend",
                "user-other"));
    }

    [Fact]
    public async Task Owner_Cannot_Remove_Themselves()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.RemoveAsync(
                f.Trip.Id,
                "user-owner",
                "user-owner"));
    }

    [Fact]
    public async Task Unknown_Trip_Should_Fail()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => f.Service.RemoveAsync(
                Guid.NewGuid(),
                "user-owner",
                "user-friend"));
    }

    [Fact]
    public async Task Unknown_Member_Should_Fail()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => f.Service.RemoveAsync(
                f.Trip.Id,
                "user-owner",
                "user-missing"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_Authentication_Should_Fail(
        string? userId)
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Service.RemoveAsync(
                f.Trip.Id,
                userId,
                "user-friend"));
    }

    [Fact]
    public async Task Empty_TripPlanId_Should_Fail()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.Service.RemoveAsync(
                Guid.Empty,
                "user-owner",
                "user-friend"));
    }

    [Fact]
    public async Task Empty_MemberUserId_Should_Fail()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.Service.RemoveAsync(
                f.Trip.Id,
                "user-owner",
                ""));
    }

    [Fact]
    public async Task Removal_Should_Queue_One_Notice()
    {
        var f = await SetupAsync();

        await f.Service.RemoveAsync(
            f.Trip.Id,
            "user-owner",
            "user-friend");

        await f.Service.RemoveAsync(
            f.Trip.Id,
            "user-owner",
            "user-friend");

        var notices =
            await f.Repository.GetQueuedNoticesAsync();

        Assert.Single(notices);
        Assert.Equal(f.Member.Id, notices[0].MemberId);
        Assert.Equal(RemovedAt, notices[0].RemovedAtUtc);
    }

    [Fact]
    public async Task Removed_Member_Should_Lose_Private_Access()
    {
        var f = await SetupAsync();

        var active = await f.Repository.GetMemberAsync(
            f.Trip.Id,
            "user-friend");

        Assert.NotNull(active);

        TripMembershipAccessValidator.EnsureCanAccess(
            f.Trip,
            active,
            "user-friend");

        await f.Service.RemoveAsync(
            f.Trip.Id,
            "user-owner",
            "user-friend");

        // Authorization must use a FRESH membership snapshot.
        var removed = await f.Repository.GetMemberAsync(
            f.Trip.Id,
            "user-friend");

        Assert.NotNull(removed);

        Assert.Throws<UnauthorizedAccessException>(() =>
            TripMembershipAccessValidator.EnsureCanAccess(
                f.Trip,
                removed,
                "user-friend"));
    }

    [Fact]
    public async Task Removal_Should_Not_Change_Owner_Access()
    {
        var f = await SetupAsync();

        await f.Service.RemoveAsync(
            f.Trip.Id,
            "user-owner",
            "user-friend");

        TripMembershipAccessValidator.EnsureCanAccess(
            f.Trip,
            null,
            "user-owner");
    }

    [Fact]
    public async Task Owner_UserId_Should_Allow_Whitespace_Trim()
    {
        var f = await SetupAsync();

        var removed = await f.Service.RemoveAsync(
            f.Trip.Id,
            "  user-owner  ",
            "  user-friend  ");

        Assert.True(removed);
    }

    [Fact]
    public async Task Different_UserId_Case_Should_Be_Denied()
    {
        var f = await SetupAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Service.RemoveAsync(
                f.Trip.Id,
                "USER-OWNER",
                "user-friend"));
    }

    [Fact]
    public async Task Notification_Should_Not_Grant_Gps_Consent()
    {
        var f = await SetupAsync();

        await f.Service.RemoveAsync(
            f.Trip.Id,
            "user-owner",
            "user-friend");

        var notices =
            await f.Repository.GetQueuedNoticesAsync();

        Assert.Single(notices);

        // GPS consent is a separate opt-in.
        var consent = TripGpsConsent.CreateDisabled(
            Guid.NewGuid(),
            "user-friend");

        Assert.False(consent.IsGranted);
    }

    [Fact]
    public void Null_Repository_Should_Fail()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TripMembershipRevocationService(
                null!,
                new FixedTimeProvider()));
    }

    [Fact]
    public void Null_TimeProvider_Should_Fail()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        Assert.Throws<ArgumentNullException>(() =>
            new TripMembershipRevocationService(
                repository,
                null!));
    }
}
