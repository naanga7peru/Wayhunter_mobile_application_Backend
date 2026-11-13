
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class InMemoryLiveTripSessionRepositoryTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

    private static LiveTripSession CreateSession(
        string tripPlanId = "plan-001")
    {
        return LiveTripSession.Start(
            Guid.NewGuid(),
            tripPlanId,
            StartTime);
    }

    [Fact]
    public async Task Add_Should_Save_Session()
    {
        var repository = new InMemoryLiveTripSessionRepository();
        var session = CreateSession();

        await repository.AddAsync(session);

        var saved = await repository.GetByIdAsync(session.SessionId);

        Assert.NotNull(saved);
        Assert.Equal(session.SessionId, saved.SessionId);
        Assert.Equal("plan-001", saved.TripPlanId);
    }

    [Fact]
    public async Task Unknown_Session_Should_Return_Null()
    {
        var repository = new InMemoryLiveTripSessionRepository();

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOpen_Should_Find_Active_Session()
    {
        var repository = new InMemoryLiveTripSessionRepository();
        var session = CreateSession();

        await repository.AddAsync(session);

        var result = await repository.GetOpenByTripPlanIdAsync(
            "plan-001");

        Assert.NotNull(result);
        Assert.Equal(LiveTripStatus.Active, result.Status);
    }

    [Fact]
    public async Task GetOpen_Should_Find_Paused_Session()
    {
        var repository = new InMemoryLiveTripSessionRepository();
        var session = CreateSession();

        session.Pause();

        await repository.AddAsync(session);

        var result = await repository.GetOpenByTripPlanIdAsync(
            "plan-001");

        Assert.NotNull(result);
        Assert.Equal(LiveTripStatus.Paused, result.Status);
    }

    [Fact]
    public async Task Finished_Session_Should_Not_Be_Open()
    {
        var repository = new InMemoryLiveTripSessionRepository();
        var session = CreateSession();

        session.Finish(StartTime.AddHours(1));

        await repository.AddAsync(session);

        var result = await repository.GetOpenByTripPlanIdAsync(
            "plan-001");

        Assert.Null(result);
    }

    [Fact]
    public async Task Duplicate_SessionId_Should_Fail()
    {
        var repository = new InMemoryLiveTripSessionRepository();
        var session = CreateSession();

        await repository.AddAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await repository.AddAsync(session));
    }

    [Fact]
    public async Task Duplicate_Active_Trip_Should_Fail()
    {
        var repository = new InMemoryLiveTripSessionRepository();

        var first = CreateSession("plan-001");
        var second = CreateSession("plan-001");

        await repository.AddAsync(first);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await repository.AddAsync(second));
    }

    [Fact]
    public async Task Duplicate_Paused_Trip_Should_Fail()
    {
        var repository = new InMemoryLiveTripSessionRepository();

        var first = CreateSession("plan-001");
        first.Pause();

        var second = CreateSession("plan-001");

        await repository.AddAsync(first);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await repository.AddAsync(second));
    }

    [Fact]
    public async Task Finished_Trip_Can_Start_New_Session()
    {
        var repository = new InMemoryLiveTripSessionRepository();

        var first = CreateSession("plan-001");
        first.Finish(StartTime.AddHours(1));

        await repository.AddAsync(first);

        var second = CreateSession("plan-001");

        await repository.AddAsync(second);

        var result = await repository.GetOpenByTripPlanIdAsync(
            "plan-001");

        Assert.NotNull(result);
        Assert.Equal(second.SessionId, result.SessionId);
    }

    [Fact]
    public async Task Update_Unknown_Session_Should_Fail()
    {
        var repository = new InMemoryLiveTripSessionRepository();
        var session = CreateSession();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await repository.UpdateAsync(session));
    }

    [Fact]
    public async Task Update_Should_Keep_Paused_Status()
    {
        var repository = new InMemoryLiveTripSessionRepository();
        var session = CreateSession();

        await repository.AddAsync(session);

        session.Pause();

        await repository.UpdateAsync(session);

        var saved = await repository.GetByIdAsync(session.SessionId);

        Assert.NotNull(saved);
        Assert.Equal(LiveTripStatus.Paused, saved.Status);
    }

    [Fact]
    public async Task Empty_TripPlanId_Lookup_Should_Fail()
    {
        var repository = new InMemoryLiveTripSessionRepository();

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await repository.GetOpenByTripPlanIdAsync(
                "  "));
    }

    [Fact]
    public async Task Cancelled_Request_Should_Fail()
    {
        var repository = new InMemoryLiveTripSessionRepository();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var session = CreateSession();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await repository.AddAsync(
                session,
                cancellation.Token));
    }
}
