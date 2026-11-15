
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class InMemoryTripMembershipRevocationRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset JoinedAt =
        CreatedAt.AddMinutes(5);

    private static readonly DateTimeOffset RemovedAt =
        JoinedAt.AddHours(1);

    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "user-owner",
            "Jaffna Trip",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Van",
            4);
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

    [Fact]
    public async Task Add_And_Get_Member_Should_Work()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        var stored = await repository.GetMemberAsync(
            trip.Id,
            "user-friend");

        Assert.NotNull(stored);
        Assert.Equal(member.Id, stored.Id);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Duplicate_Active_Member_Should_Fail()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(
            CreateMember(trip.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.AddMemberAsync(
                CreateMember(trip.Id)));
    }

    [Fact]
    public async Task Add_Member_For_Unknown_Trip_Should_Fail()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        await Assert.ThrowsAsync<
            System.Collections.Generic.KeyNotFoundException>(
            () => repository.AddMemberAsync(
                CreateMember(Guid.NewGuid())));
    }

    [Fact]
    public async Task Remove_Should_Deactivate_Member()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        var changed =
            await repository.RemoveMemberAndQueueNotificationAsync(
                trip.Id,
                member.Id,
                RemovedAt);

        var stored = await repository.GetMemberAsync(
            trip.Id,
            "user-friend");

        Assert.True(changed);
        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
        Assert.False(stored.CanAccessPrivateTrip());
        Assert.Equal(RemovedAt, stored.RemovedAtUtc);
    }

    [Fact]
    public async Task Removal_Should_Queue_One_Notification()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        await repository.RemoveMemberAndQueueNotificationAsync(
            trip.Id,
            member.Id,
            RemovedAt);

        var notices = await repository.GetQueuedNoticesAsync();

        Assert.Single(notices);
        Assert.Equal(trip.Id, notices[0].TripPlanId);
        Assert.Equal(member.Id, notices[0].MemberId);
        Assert.Equal("user-friend", notices[0].MemberUserId);
        Assert.Equal(RemovedAt, notices[0].RemovedAtUtc);
    }

    [Fact]
    public async Task Repeated_Remove_Should_Not_Queue_Duplicate()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        var first =
            await repository.RemoveMemberAndQueueNotificationAsync(
                trip.Id,
                member.Id,
                RemovedAt);

        var second =
            await repository.RemoveMemberAndQueueNotificationAsync(
                trip.Id,
                member.Id,
                RemovedAt.AddMinutes(5));

        Assert.True(first);
        Assert.False(second);

        Assert.Single(
            await repository.GetQueuedNoticesAsync());
    }

    [Fact]
    public async Task Wrong_Trip_Should_Not_Remove_Member()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        await Assert.ThrowsAsync<
            System.Collections.Generic.KeyNotFoundException>(
            () => repository.RemoveMemberAndQueueNotificationAsync(
                Guid.NewGuid(),
                member.Id,
                RemovedAt));

        var stored = await repository.GetMemberAsync(
            trip.Id,
            "user-friend");

        Assert.NotNull(stored);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Retrieved_Member_Should_Not_Mutate_Storage()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        var copy = await repository.GetMemberAsync(
            trip.Id,
            "user-friend");

        Assert.NotNull(copy);

        copy.Remove(RemovedAt);

        var stored = await repository.GetMemberAsync(
            trip.Id,
            "user-friend");

        Assert.NotNull(stored);
        Assert.True(stored.IsActive);
        Assert.Empty(
            await repository.GetQueuedNoticesAsync());
    }

    [Fact]
    public async Task Removal_Before_Join_Should_Not_Change_State()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.RemoveMemberAndQueueNotificationAsync(
                trip.Id,
                member.Id,
                CreatedAt));

        var stored = await repository.GetMemberAsync(
            trip.Id,
            "user-friend");

        Assert.NotNull(stored);
        Assert.True(stored.IsActive);
        Assert.Empty(
            await repository.GetQueuedNoticesAsync());
    }

    [Fact]
    public async Task Concurrent_Removal_Should_Only_Queue_Once()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        var operations = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
                await repository.RemoveMemberAndQueueNotificationAsync(
                    trip.Id,
                    member.Id,
                    RemovedAt)));

        var results = await Task.WhenAll(operations);

        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(9, results.Count(x => !x));

        Assert.Single(
            await repository.GetQueuedNoticesAsync());
    }

    [Fact]
    public async Task Cancelled_Operation_Should_Not_Remove()
    {
        var repository =
            new InMemoryTripMembershipRevocationRepository();

        var trip = CreateTrip();
        var member = CreateMember(trip.Id);

        await repository.AddTripAsync(trip);
        await repository.AddMemberAsync(member);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.RemoveMemberAndQueueNotificationAsync(
                trip.Id,
                member.Id,
                RemovedAt,
                cts.Token));

        Assert.Empty(
            await repository.GetQueuedNoticesAsync());
    }
}
