
using System;
using System.Linq;
using System.Threading.Tasks;
using Wayhunter.Infrastructure.Integrations.Maps;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class InMemoryNearbyPlacesDailyQuotaTests
{
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } =
            new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static string NewUserId() =>
        Guid.NewGuid().ToString("D");

    [Fact]
    public void SameUser_CannotExceed20Requests()
    {
        var quota = new InMemoryNearbyPlacesDailyQuota(
            new TestClock());

        var userId = NewUserId();

        for (var i = 0; i < 20; i++)
        {
            Assert.True(quota.TryAcquire(userId));
        }

        Assert.False(quota.TryAcquire(userId));
        Assert.False(quota.TryAcquire(userId));
    }

    [Fact]
    public void DifferentUsers_HaveSeparateLimits()
    {
        var quota = new InMemoryNearbyPlacesDailyQuota(
            new TestClock());

        var firstUser = NewUserId();
        var secondUser = NewUserId();

        Assert.True(quota.TryAcquire(firstUser, 1));
        Assert.False(quota.TryAcquire(firstUser, 1));

        Assert.True(quota.TryAcquire(secondUser, 1));
        Assert.False(quota.TryAcquire(secondUser, 1));
    }

    [Fact]
    public void NewUtcDay_ResetsRequestLimit()
    {
        var clock = new TestClock
        {
            Now = new DateTimeOffset(
                2026, 11, 20, 23, 59, 0,
                TimeSpan.Zero)
        };

        var quota = new InMemoryNearbyPlacesDailyQuota(clock);
        var userId = NewUserId();

        Assert.True(quota.TryAcquire(userId, 1));
        Assert.False(quota.TryAcquire(userId, 1));

        clock.Now = new DateTimeOffset(
            2026, 11, 21, 0, 1, 0,
            TimeSpan.Zero);

        Assert.True(quota.TryAcquire(userId, 1));
    }

    [Fact]
    public async Task ConcurrentRequests_CannotExceedLimit()
    {
        var quota = new InMemoryNearbyPlacesDailyQuota(
            new TestClock());

        var userId = NewUserId();

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(
                () => quota.TryAcquire(userId, 20)));

        var results = await Task.WhenAll(tasks);

        Assert.Equal(20, results.Count(x => x));
        Assert.Equal(80, results.Count(x => !x));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void InvalidIdentity_IsRejected(string userId)
    {
        var quota = new InMemoryNearbyPlacesDailyQuota(
            new TestClock());

        Assert.Throws<ArgumentException>(
            () => quota.TryAcquire(userId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InvalidDailyLimit_IsRejected(int limit)
    {
        var quota = new InMemoryNearbyPlacesDailyQuota(
            new TestClock());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => quota.TryAcquire(NewUserId(), limit));
    }
}
