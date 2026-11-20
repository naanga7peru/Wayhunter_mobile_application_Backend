
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Wayhunter.Api.Extensions;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3Batch2SafetyTests
{
    private static readonly DateTimeOffset Started =
        new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Now =
        Started.AddHours(2);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeReminderSender
        : ITripStartReminderSender
    {
        public int SendCount { get; private set; }

        public Task SendAsync(
            Guid tripPlanId,
            string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        public string Owner { get; } =
            Guid.NewGuid().ToString("D");

        public string Member { get; } =
            Guid.NewGuid().ToString("D");

        public string Outsider { get; } =
            Guid.NewGuid().ToString("D");

        public TripPlan Trip { get; }

        public LiveTripSession Session { get; private set; } = null!;

        public InMemoryLiveTripSessionRepository Sessions { get; } =
            new();

        public InMemoryTripMembershipRevocationRepository Members { get; } =
            new();

        public InMemoryTripSafetyCheckInStore Store { get; } =
            new();

        public InMemoryTripFinishedReminderGuard ReminderGuard { get; } =
            new();

        public TripSafeCheckInService CheckIns { get; private set; }
            = null!;

        public LiveTripSessionService SessionService { get; private set; }
            = null!;

        public Fixture()
        {
            Trip = new TripPlan(
                Owner,
                "Batch 2 Safety Trip",
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 11, 21),
                5000m,
                "Car",
                2);
        }

        public async Task InitializeAsync()
        {
            await Members.AddTripAsync(Trip);

            Session = LiveTripSession.Start(
                Guid.NewGuid(),
                Trip.Id,
                Started);

            await Sessions.AddAsync(Session);

            CheckIns = new TripSafeCheckInService(
                Members,
                Sessions,
                Store,
                new FixedClock());

            SessionService = new LiveTripSessionService(
                Sessions,
                new FixedClock());
        }

        public async Task AddActiveMemberAsync()
        {
            var invitation = TripInvitation.Create(
                Guid.NewGuid(),
                Trip.Id,
                Owner,
                Member,
                Started);

            invitation.Accept(Started.AddMinutes(5));

            var member = TripMember.FromAcceptedInvitation(
                Guid.NewGuid(),
                invitation);

            await Members.AddMemberAsync(member);
        }
    }

    private static async Task<Fixture> CreateAsync()
    {
        var fixture = new Fixture();
        await fixture.InitializeAsync();
        return fixture;
    }

    [Fact]
    public async Task Owner_Can_Mark_Safe()
    {
        var f = await CreateAsync();

        var result = await f.CheckIns.MarkSafeAsync(
            f.Trip.Id,
            f.Session.SessionId,
            f.Owner);

        Assert.True(result.Created);
        Assert.Equal(f.Owner, result.CheckIn.UserId);
        Assert.Equal(f.Trip.Id, result.CheckIn.TripPlanId);
        Assert.Equal(f.Session.SessionId, result.CheckIn.SessionId);
        Assert.Equal(TimeSpan.Zero,
            result.CheckIn.CheckedInAtUtc.Offset);
    }

    [Fact]
    public async Task ActiveMember_Can_Mark_Safe()
    {
        var f = await CreateAsync();

        await f.AddActiveMemberAsync();

        var result = await f.CheckIns.MarkSafeAsync(
            f.Trip.Id,
            f.Session.SessionId,
            f.Member);

        Assert.True(result.Created);
        Assert.Equal(f.Member, result.CheckIn.UserId);
    }

    [Fact]
    public async Task Outsider_Cannot_Mark_Safe()
    {
        var f = await CreateAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.CheckIns.MarkSafeAsync(
                f.Trip.Id,
                f.Session.SessionId,
                f.Outsider));
    }

    [Fact]
    public async Task Missing_Trusted_Identity_Is_Denied()
    {
        var f = await CreateAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.CheckIns.MarkSafeAsync(
                f.Trip.Id,
                f.Session.SessionId,
                "not-a-guid"));
    }

    [Fact]
    public async Task Removed_Member_Cannot_Mark_Safe()
    {
        var f = await CreateAsync();

        await f.AddActiveMemberAsync();

        var member = await f.Members.GetMemberAsync(
            f.Trip.Id,
            f.Member);

        Assert.NotNull(member);

        var removed =
            await f.Members.RemoveMemberAndQueueNotificationAsync(
                f.Trip.Id,
                member.Id,
                Now);

        Assert.True(removed);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.CheckIns.MarkSafeAsync(
                f.Trip.Id,
                f.Session.SessionId,
                f.Member));
    }

    [Fact]
    public async Task Wrong_Session_Is_Rejected()
    {
        var f = await CreateAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => f.CheckIns.MarkSafeAsync(
                f.Trip.Id,
                Guid.NewGuid(),
                f.Owner));
    }

    [Fact]
    public async Task Finished_Session_Cannot_Mark_Safe()
    {
        var f = await CreateAsync();

        await f.SessionService.FinishAsync(
            f.Session.SessionId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.CheckIns.MarkSafeAsync(
                f.Trip.Id,
                f.Session.SessionId,
                f.Owner));
    }

    [Fact]
    public async Task Cancelled_Trip_Is_Rejected()
    {
        var f = await CreateAsync();

        f.Trip.Cancel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.CheckIns.MarkSafeAsync(
                f.Trip.Id,
                f.Session.SessionId,
                f.Owner));
    }

    [Fact]
    public async Task Repeated_CheckIn_Is_Idempotent()
    {
        var f = await CreateAsync();

        var first = await f.CheckIns.MarkSafeAsync(
            f.Trip.Id,
            f.Session.SessionId,
            f.Owner);

        var second = await f.CheckIns.MarkSafeAsync(
            f.Trip.Id,
            f.Session.SessionId,
            f.Owner);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(
            first.CheckIn.CheckInId,
            second.CheckIn.CheckInId);
    }

    [Fact]
    public async Task Concurrent_CheckIns_Only_Create_One_Event()
    {
        var f = await CreateAsync();

        var calls = Enumerable.Range(0, 12)
            .Select(_ => f.CheckIns.MarkSafeAsync(
                f.Trip.Id,
                f.Session.SessionId,
                f.Owner));

        var results = await Task.WhenAll(calls);

        Assert.Single(results, x => x.Created);

        Assert.Single(
            results.Select(x => x.CheckIn.CheckInId).Distinct());
    }

    [Fact]
    public async Task CheckIn_Does_Not_Finish_Trip()
    {
        var f = await CreateAsync();

        await f.CheckIns.MarkSafeAsync(
            f.Trip.Id,
            f.Session.SessionId,
            f.Owner);

        var session = await f.Sessions.GetByIdAsync(
            f.Session.SessionId);

        Assert.NotNull(session);
        Assert.Equal(LiveTripStatus.Active, session.Status);
    }

    [Fact]
    public async Task CheckIn_Does_Not_Remove_Membership()
    {
        var f = await CreateAsync();

        await f.AddActiveMemberAsync();

        await f.CheckIns.MarkSafeAsync(
            f.Trip.Id,
            f.Session.SessionId,
            f.Member);

        var member = await f.Members.GetMemberAsync(
            f.Trip.Id,
            f.Member);

        Assert.NotNull(member);
        Assert.True(member.IsActive);
    }

    [Fact]
    public async Task Paused_Trip_Can_Accept_Safe_CheckIn()
    {
        var f = await CreateAsync();

        await f.SessionService.PauseAsync(
            f.Session.SessionId);

        var result = await f.CheckIns.MarkSafeAsync(
            f.Trip.Id,
            f.Session.SessionId,
            f.Owner);

        Assert.True(result.Created);
    }

    [Fact]
    public async Task Reminder_Is_Suppressed_After_Finish()
    {
        var f = await CreateAsync();

        var sender = new FakeReminderSender();

        var dispatch = new TripStartReminderDispatchService(
            sender,
            f.ReminderGuard);

        await dispatch.DispatchAsync(
            f.Trip.Id,
            f.Owner);

        Assert.Equal(1, sender.SendCount);

        await f.SessionService.FinishAsync(
            f.Session.SessionId);

        await f.ReminderGuard.MarkFinishedAsync(
            f.Trip.Id,
            f.Session.SessionId);

        await dispatch.DispatchAsync(
            f.Trip.Id,
            f.Owner);

        Assert.Equal(1, sender.SendCount);
    }

    [Fact]
    public async Task Reminder_Cancellation_Is_Idempotent()
    {
        var f = await CreateAsync();

        await f.ReminderGuard.MarkFinishedAsync(
            f.Trip.Id,
            f.Session.SessionId);

        await f.ReminderGuard.MarkFinishedAsync(
            f.Trip.Id,
            f.Session.SessionId);

        Assert.True(
            await f.ReminderGuard.HasFinishedAsync(
                f.Trip.Id));
    }

    [Fact]
    public async Task Unavailable_Sql_CheckIn_Fails_Closed()
    {
        var store = new UnavailableTripSafetyCheckInStore();

        var checkIn = new TripSafeCheckIn(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            Now);

        await Assert.ThrowsAsync<
            TripSafetyCheckInUnavailableException>(
            () => store.RecordAsync(checkIn));
    }

    [Fact]
    public void Sql_Mode_Registers_FailClosed_CheckIn_Store()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TripSafety:EnableSqlPersistence"] = "true"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddTripSafetyBatch2Services(config);

        Assert.Contains(
            services,
            x => x.ServiceType ==
                    typeof(ITripSafetyCheckInStore) &&
                 x.ImplementationType ==
                    typeof(UnavailableTripSafetyCheckInStore));

        Assert.Contains(
            services,
            x => x.ServiceType ==
                    typeof(ITripFinishedReminderGuard) &&
                 x.ImplementationType ==
                    typeof(EfTripFinishedReminderGuard));
    }

    [Fact]
    public void InMemory_Mode_Registers_InMemory_Stores()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TripSafety:EnableSqlPersistence"] = "false"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddTripSafetyBatch2Services(config);

        Assert.Contains(
            services,
            x => x.ServiceType ==
                    typeof(ITripSafetyCheckInStore));

        Assert.Contains(
            services,
            x => x.ServiceType ==
                    typeof(ITripFinishedReminderGuard));
    }
}
