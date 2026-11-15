
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class LiveTripSessionServiceTests
{
    private static readonly Guid TripPlanId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static readonly Guid AnotherTripPlanId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static (
        LiveTripSessionService Service,
        FakeLiveTripSessionRepository Repository)
        CreateService()
    {
        var repository = new FakeLiveTripSessionRepository();

        var service = new LiveTripSessionService(
            repository,
            TimeProvider.System);

        return (service, repository);
    }

    [Fact]
    public async Task Start_Should_Create_Active_Session()
    {
        var (service, repository) = CreateService();

        var session = await service.StartAsync(TripPlanId);

        Assert.NotEqual(Guid.Empty, session.SessionId);
        Assert.Equal(TripPlanId, session.TripPlanId);
        Assert.Equal(LiveTripStatus.Active, session.Status);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task Start_With_Different_TripPlanId_Should_Create_Different_Session()
    {
        var (service, repository) = CreateService();

        var first = await service.StartAsync(TripPlanId);
        var second = await service.StartAsync(AnotherTripPlanId);

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(TripPlanId, first.TripPlanId);
        Assert.Equal(AnotherTripPlanId, second.TripPlanId);
        Assert.Equal(2, repository.AddCount);
    }

    [Fact]
    public async Task Start_With_Empty_Plan_Should_Fail()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.StartAsync(Guid.Empty));
    }

    [Fact]
    public async Task Repeated_Start_Should_Return_Existing_Active_Session()
    {
        var (service, repository) = CreateService();

        var first = await service.StartAsync(TripPlanId);
        var second = await service.StartAsync(TripPlanId);

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Same(first, second);
        Assert.Equal(LiveTripStatus.Active, second.Status);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task Repeated_Start_Should_Return_Existing_Paused_Session()
    {
        var (service, repository) = CreateService();

        var first = await service.StartAsync(TripPlanId);

        await service.PauseAsync(first.SessionId);

        var second = await service.StartAsync(TripPlanId);

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Same(first, second);
        Assert.Equal(LiveTripStatus.Paused, second.Status);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task Pause_Should_Change_Status()
    {
        var (service, repository) = CreateService();

        var session = await service.StartAsync(TripPlanId);

        var paused = await service.PauseAsync(session.SessionId);

        Assert.Equal(LiveTripStatus.Paused, paused.Status);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task Resume_Should_Change_Status()
    {
        var (service, repository) = CreateService();

        var session = await service.StartAsync(TripPlanId);

        await service.PauseAsync(session.SessionId);

        var resumed = await service.ResumeAsync(session.SessionId);

        Assert.Equal(LiveTripStatus.Active, resumed.Status);
        Assert.Equal(2, repository.UpdateCount);
    }

    [Fact]
    public async Task Finish_Should_Change_Status()
    {
        var (service, repository) = CreateService();

        var session = await service.StartAsync(TripPlanId);

        var finished = await service.FinishAsync(session.SessionId);

        Assert.Equal(LiveTripStatus.Finished, finished.Status);
        Assert.NotNull(finished.FinishedAtUtc);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task Paused_Trip_Can_Be_Finished()
    {
        var (service, _) = CreateService();

        var session = await service.StartAsync(TripPlanId);

        await service.PauseAsync(session.SessionId);

        var finished = await service.FinishAsync(session.SessionId);

        Assert.Equal(LiveTripStatus.Finished, finished.Status);
    }

    [Fact]
    public async Task Duplicate_Finish_Should_Not_Update_Again()
    {
        var (service, repository) = CreateService();

        var session = await service.StartAsync(TripPlanId);

        var first = await service.FinishAsync(session.SessionId);

        var firstFinishTime = first.FinishedAtUtc;

        var second = await service.FinishAsync(session.SessionId);

        Assert.Equal(firstFinishTime, second.FinishedAtUtc);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task Unknown_Session_Should_Fail()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.PauseAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Empty_SessionId_Should_Fail()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ResumeAsync(Guid.Empty));
    }

    [Fact]
    public async Task Finished_Plan_Can_Start_New_Session()
    {
        var (service, repository) = CreateService();

        var first = await service.StartAsync(TripPlanId);

        await service.FinishAsync(first.SessionId);

        var second = await service.StartAsync(TripPlanId);

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(LiveTripStatus.Active, second.Status);
        Assert.Equal(2, repository.AddCount);
    }

    // Fake repository for unit tests only.
    // No SQL Server connection is required.
    private sealed class FakeLiveTripSessionRepository
        : ILiveTripSessionRepository
    {
        private readonly Dictionary<Guid, LiveTripSession>
            _sessions = new();

        public int AddCount { get; private set; }

        public int UpdateCount { get; private set; }

        public Task<LiveTripSession?> GetByIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _sessions.TryGetValue(
                sessionId,
                out var session);

            return Task.FromResult<LiveTripSession?>(session);
        }

        public Task<LiveTripSession?> GetOpenByTripPlanIdAsync(
            Guid tripPlanId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var session in _sessions.Values)
            {
                if (session.TripPlanId == tripPlanId &&
                    session.Status != LiveTripStatus.Finished)
                {
                    return Task.FromResult<LiveTripSession?>(
                        session);
                }
            }

            return Task.FromResult<LiveTripSession?>(null);
        }

        public Task AddAsync(
            LiveTripSession session,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _sessions.Add(session.SessionId, session);
            AddCount++;

            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            LiveTripSession session,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _sessions[session.SessionId] = session;
            UpdateCount++;

            return Task.CompletedTask;
        }
    }
}
