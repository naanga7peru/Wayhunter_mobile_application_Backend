
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class ConcurrentLiveTripStartTests
{
    [Fact]
    public async Task Concurrent_Start_Same_Plan_Should_Return_Same_Session()
    {
        var repository = new TwoRequestBarrierRepository();

        var service = new LiveTripSessionService(
            repository,
            TimeProvider.System);

        var firstTask = service.StartAsync("plan-001");
        var secondTask = service.StartAsync("  plan-001  ");

        var results = await Task.WhenAll(
            firstTask,
            secondTask);

        Assert.Equal(
            results[0].SessionId,
            results[1].SessionId);

        Assert.Same(results[0], results[1]);

        Assert.Equal(1, repository.AddedCount);
    }

    [Fact]
    public async Task Concurrent_Different_Plans_Should_Create_Two_Sessions()
    {
        var repository = new TwoRequestBarrierRepository();

        var service = new LiveTripSessionService(
            repository,
            TimeProvider.System);

        var firstTask = service.StartAsync("plan-001");
        var secondTask = service.StartAsync("plan-002");

        var results = await Task.WhenAll(
            firstTask,
            secondTask);

        Assert.NotEqual(
            results[0].SessionId,
            results[1].SessionId);

        Assert.Equal("plan-001", results[0].TripPlanId);
        Assert.Equal("plan-002", results[1].TripPlanId);
        Assert.Equal(2, repository.AddedCount);
    }

    // Force both requests to observe no existing session
    // before either is allowed to insert.
    private sealed class TwoRequestBarrierRepository
        : ILiveTripSessionRepository
    {
        private readonly InMemoryLiveTripSessionRepository
            _inner = new();

        private readonly TaskCompletionSource<bool>
            _bothReadsComplete = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private int _readCount;
        private int _addedCount;

        public int AddedCount =>
            Volatile.Read(ref _addedCount);

        public async Task<LiveTripSession?>
            GetOpenByTripPlanIdAsync(
                string tripPlanId,
                CancellationToken cancellationToken = default)
        {
            int readNumber =
                Interlocked.Increment(ref _readCount);

            if (readNumber <= 2)
            {
                if (readNumber == 2)
                {
                    _bothReadsComplete.TrySetResult(true);
                }

                await _bothReadsComplete.Task.WaitAsync(
                    cancellationToken);

                // Both original reads see the same
                // "no session yet" snapshot.
                return null;
            }

            return await _inner.GetOpenByTripPlanIdAsync(
                tripPlanId,
                cancellationToken);
        }

        public Task<LiveTripSession?> GetByIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetByIdAsync(
                sessionId,
                cancellationToken);
        }

        public async Task AddAsync(
            LiveTripSession session,
            CancellationToken cancellationToken = default)
        {
            await _inner.AddAsync(
                session,
                cancellationToken);

            Interlocked.Increment(ref _addedCount);
        }

        public Task UpdateAsync(
            LiveTripSession session,
            CancellationToken cancellationToken = default)
        {
            return _inner.UpdateAsync(
                session,
                cancellationToken);
        }
    }
}
