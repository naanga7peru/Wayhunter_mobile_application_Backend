
using System;
using System.Linq;
using System.Threading.Tasks;
using Wayhunter.Infrastructure.Integrations.Ai;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class InMemoryAiTripDailyQuotaTests
{
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now =
            new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Set(DateTimeOffset value)
        {
            _now = value;
        }
    }

    private static readonly string User1 =
        Guid.NewGuid().ToString("D");

    private static readonly string User2 =
        Guid.NewGuid().ToString("D");

    [Fact]
    public void SameUser_CannotExceedDailyLimit()
    {
        var quota = new InMemoryAiTripDailyQuota(
            new TestClock());

        Assert.True(quota.TryAcquire(User1, 2));
        Assert.True(quota.TryAcquire(User1, 2));
        Assert.False(quota.TryAcquire(User1, 2));
    }

    [Fact]
    public void DifferentUsers_HaveIndependentQuotas()
    {
        var quota = new InMemoryAiTripDailyQuota(
            new TestClock());

        Assert.True(quota.TryAcquire(User1, 1));
        Assert.False(quota.TryAcquire(User1, 1));

        Assert.True(quota.TryAcquire(User2, 1));
    }

    [Fact]
    public void NewUtcDay_ResetsQuota()
    {
        var clock = new TestClock();

        clock.Set(new DateTimeOffset(
            2026, 11, 19, 23, 59, 0,
            TimeSpan.Zero));

        var quota = new InMemoryAiTripDailyQuota(clock);

        Assert.True(quota.TryAcquire(User1, 1));
        Assert.False(quota.TryAcquire(User1, 1));

        clock.Set(new DateTimeOffset(
            2026, 11, 20, 0, 1, 0,
            TimeSpan.Zero));

        Assert.True(quota.TryAcquire(User1, 1));
    }

    [Fact]
    public async Task ConcurrentRequests_CannotExceedLimit()
    {
        var quota = new InMemoryAiTripDailyQuota(
            new TestClock());

        var tasks = Enumerable.Range(0, 40)
            .Select(_ => Task.Run(
                () => quota.TryAcquire(User1, 5)));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Count(x => x));
        Assert.Equal(35, results.Count(x => !x));
    }

    [Fact]
    public void InvalidUserIdentity_IsRejected()
    {
        var quota = new InMemoryAiTripDailyQuota(
            new TestClock());

        Assert.Throws<ArgumentException>(
            () => quota.TryAcquire("fake-user", 10));
    }
}
