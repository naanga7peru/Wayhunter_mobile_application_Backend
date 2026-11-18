
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class EfTripMembershipRevocationRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        JoinedAt.AddHours(1);

    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(
                    $"m3-revocation-{Guid.NewGuid()}")
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(
                        InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "user-owner",
            "M3 Group Trip",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Van",
            5);
    }

    private static TripMember CreateMember(Guid tripPlanId)
    {
        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            tripPlanId,
            "user-owner",
            "user-friend",
            CreatedAt);

        invitation.Accept(JoinedAt);

        return TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);
    }

    private static async Task<(
        WayhunterSharedDbContext Context,
        EfTripMembershipRevocationRepository Repository,
        TripPlan Trip,
        TripMember Member)> CreateFixtureAsync()
    {
        var context = CreateContext();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        context.Trips.Add(trip);
        context.TripMembers.Add(member);

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var repository =
            new EfTripMembershipRevocationRepository(context);

        return (context, repository, trip, member);
    }

    [Fact]
    public async Task GetTrip_Should_Return_Persisted_Trip()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        var result =
            await fixture.Repository.GetTripPlanByIdAsync(
                fixture.Trip.Id);

        Assert.NotNull(result);
        Assert.Equal(fixture.Trip.Id, result.Id);
        Assert.Equal("user-owner", result.OwnerUserId);
    }

    [Fact]
    public async Task GetMember_Should_Return_Active_Member()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        var result = await fixture.Repository.GetMemberAsync(
            fixture.Trip.Id,
            "user-friend");

        Assert.NotNull(result);
        Assert.Equal(fixture.Member.Id, result.Id);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Remove_Should_Deactivate_Member()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        var removed =
            await fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    fixture.Trip.Id,
                    fixture.Member.Id,
                    RemovedAt);

        Assert.True(removed);

        var stored = await context.TripMembers
            .AsNoTracking()
            .SingleAsync(x => x.Id == fixture.Member.Id);

        Assert.False(stored.IsActive);
        Assert.Equal(RemovedAt, stored.RemovedAtUtc);
        Assert.False(stored.CanAccessPrivateTrip());
    }

    [Fact]
    public async Task Remove_Should_Create_One_Outbox_Event()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        await fixture.Repository
            .RemoveMemberAndQueueNotificationAsync(
                fixture.Trip.Id,
                fixture.Member.Id,
                RemovedAt);

        var events = await context.TripMembershipRevocationOutbox
            .AsNoTracking()
            .ToListAsync();

        var notification = Assert.Single(events);

        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(fixture.Trip.Id, notification.TripPlanId);
        Assert.Equal(fixture.Member.Id, notification.MemberId);
        Assert.Equal(
            fixture.Member.MemberUserId,
            notification.MemberUserId);

        Assert.Equal(RemovedAt, notification.RemovedAtUtc);
        Assert.Equal(RemovedAt, notification.CreatedAtUtc);
        Assert.False(notification.IsDelivered);
        Assert.Null(notification.DeliveredAtUtc);
        Assert.Equal(0, notification.DeliveryAttempts);
    }

    [Fact]
    public async Task Repeated_Remove_Should_Be_Idempotent()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        var first =
            await fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    fixture.Trip.Id,
                    fixture.Member.Id,
                    RemovedAt);

        var second =
            await fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    fixture.Trip.Id,
                    fixture.Member.Id,
                    RemovedAt.AddMinutes(5));

        Assert.True(first);
        Assert.False(second);

        Assert.Equal(
            1,
            await context.TripMembershipRevocationOutbox
                .CountAsync());

        var stored = await context.TripMembers
            .AsNoTracking()
            .SingleAsync(x => x.Id == fixture.Member.Id);

        Assert.Equal(RemovedAt, stored.RemovedAtUtc);
    }

    [Fact]
    public async Task Wrong_Trip_Should_Not_Remove_Member()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    Guid.NewGuid(),
                    fixture.Member.Id,
                    RemovedAt));

        var stored = await context.TripMembers
            .AsNoTracking()
            .SingleAsync(x => x.Id == fixture.Member.Id);

        Assert.True(stored.IsActive);

        Assert.Empty(
            await context.TripMembershipRevocationOutbox
                .ToListAsync());
    }

    [Fact]
    public async Task Removal_Before_Joining_Should_Fail()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    fixture.Trip.Id,
                    fixture.Member.Id,
                    CreatedAt));

        var stored = await context.TripMembers
            .AsNoTracking()
            .SingleAsync(x => x.Id == fixture.Member.Id);

        Assert.True(stored.IsActive);

        Assert.Empty(
            await context.TripMembershipRevocationOutbox
                .ToListAsync());
    }

    [Fact]
    public async Task Unknown_Member_Should_Fail()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    fixture.Trip.Id,
                    Guid.NewGuid(),
                    RemovedAt));

        Assert.Empty(
            await context.TripMembershipRevocationOutbox
                .ToListAsync());
    }

    [Fact]
    public async Task Empty_Trip_Id_Should_Fail()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    Guid.Empty,
                    fixture.Member.Id,
                    RemovedAt));
    }

    [Fact]
    public async Task Empty_Member_Id_Should_Fail()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    fixture.Trip.Id,
                    Guid.Empty,
                    RemovedAt));
    }

    [Fact]
    public async Task NonUtc_Removal_Time_Should_Fail()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        var nonUtc =
            RemovedAt.ToOffset(TimeSpan.FromHours(5));

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Repository
                .RemoveMemberAndQueueNotificationAsync(
                    fixture.Trip.Id,
                    fixture.Member.Id,
                    nonUtc));
    }

    [Fact]
    public async Task Removal_Should_Not_Create_Gps_Consent()
    {
        var fixture = await CreateFixtureAsync();
        await using var context = fixture.Context;

        await fixture.Repository
            .RemoveMemberAndQueueNotificationAsync(
                fixture.Trip.Id,
                fixture.Member.Id,
                RemovedAt);

        Assert.Empty(
            await context.TripGpsConsents.ToListAsync());
    }

    [Fact]
    public void Null_DbContext_Should_Fail()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EfTripMembershipRevocationRepository(
                null!));
    }
}
