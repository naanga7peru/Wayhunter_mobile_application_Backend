
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class LiveTripSessionRecoveryServiceTests
{
    private sealed class FakeRecoveryAccessChecker
        : ITripSessionRecoveryAccessChecker
    {
        public bool AllowAccess { get; set; } = true;
        public int CallCount { get; private set; }
        public string? LastUserId { get; private set; }
        public string? LastTripPlanId { get; private set; }

        public Task<bool> CanRecoverAsync(
            string authenticatedUserId,
            string tripPlanId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            LastUserId = authenticatedUserId;
            LastTripPlanId = tripPlanId;

            return Task.FromResult(AllowAccess);
        }
    }

    private static async Task<(
        LiveTripSessionRecoveryService Service,
        InMemoryLiveTripSessionRepository Repository,
        FakeRecoveryAccessChecker Checker,
        LiveTripSession Session)> CreateAsync(
        bool allowAccess = true)
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var checker = new FakeRecoveryAccessChecker
        {
            AllowAccess = allowAccess
        };

        var service = new LiveTripSessionRecoveryService(
            repository,
            checker);

        var session = LiveTripSession.Start(
            Guid.NewGuid(),
            "plan-001",
            new DateTimeOffset(
                2026, 11, 14, 8, 0, 0, TimeSpan.Zero));

        await repository.AddAsync(session);

        return (service, repository, checker, session);
    }

    [Fact]
    public async Task Active_Session_Should_Be_Recovered()
    {
        var (service, _, checker, session) =
            await CreateAsync();

        var result = await service.RecoverAsync(
            session.SessionId,
            "user-001");

        Assert.NotNull(result);
        Assert.Equal(session.SessionId, result.SessionId);
        Assert.Equal("plan-001", result.TripPlanId);
        Assert.Equal(LiveTripStatus.Active, result.Status);
        Assert.Equal(1, checker.CallCount);
    }

    [Fact]
    public async Task Paused_Session_Should_Be_Recovered()
    {
        var (service, _, _, session) =
            await CreateAsync();

        session.Pause();

        var result = await service.RecoverAsync(
            session.SessionId,
            "user-001");

        Assert.NotNull(result);
        Assert.Equal(LiveTripStatus.Paused, result.Status);
        Assert.Equal(session.SessionId, result.SessionId);
    }

    [Fact]
    public async Task Finished_Session_Should_Not_Be_Recovered()
    {
        var (service, _, checker, session) =
            await CreateAsync();

        session.Finish(new DateTimeOffset(
            2026, 11, 14, 9, 0, 0, TimeSpan.Zero));

        var result = await service.RecoverAsync(
            session.SessionId,
            "user-001");

        Assert.Null(result);
        Assert.Equal(1, checker.CallCount);
    }

    [Fact]
    public async Task Unauthorized_User_Should_Be_Rejected()
    {
        var (service, _, checker, session) =
            await CreateAsync(allowAccess: false);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(() =>
                service.RecoverAsync(
                    session.SessionId,
                    "unauthorized-user"));

        Assert.Equal(1, checker.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_Authentication_Should_Fail(
        string? userId)
    {
        var (service, _, checker, session) =
            await CreateAsync();

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(() =>
                service.RecoverAsync(
                    session.SessionId,
                    userId));

        Assert.Equal(0, checker.CallCount);
    }

    [Fact]
    public async Task Empty_SessionId_Should_Fail()
    {
        var (service, _, _, _) = await CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecoverAsync(
                Guid.Empty,
                "user-001"));
    }

    [Fact]
    public async Task Unknown_Session_Should_Return_Null()
    {
        var (service, _, checker, _) =
            await CreateAsync();

        var result = await service.RecoverAsync(
            Guid.NewGuid(),
            "user-001");

        Assert.Null(result);
        Assert.Equal(0, checker.CallCount);
    }

    [Fact]
    public async Task UserId_Should_Be_Trimmed_For_Access_Check()
    {
        var (service, _, checker, session) =
            await CreateAsync();

        var result = await service.RecoverAsync(
            session.SessionId,
            "  user-001  ");

        Assert.NotNull(result);
        Assert.Equal("user-001", checker.LastUserId);
        Assert.Equal("plan-001", checker.LastTripPlanId);
    }

    [Fact]
    public async Task Repeated_Recovery_Should_Use_Same_Session()
    {
        var (service, repository, _, session) =
            await CreateAsync();

        var first = await service.RecoverAsync(
            session.SessionId,
            "user-001");

        var second = await service.RecoverAsync(
            session.SessionId,
            "user-001");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.SessionId, second.SessionId);

        var stored = await repository.GetByIdAsync(
            session.SessionId);

        Assert.Same(session, stored);
    }
}
