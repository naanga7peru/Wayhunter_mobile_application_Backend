
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripSessionReconnectRegressionTests
{
    private sealed class FakeAccessChecker
        : ITripSessionRecoveryAccessChecker
    {
        public Task<bool> CanRecoverAsync(
            string authenticatedUserId,
            string tripPlanId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool allowed =
                authenticatedUserId == "user-001" &&
                tripPlanId == "plan-001";

            return Task.FromResult(allowed);
        }
    }

    private static LiveTripSessionService CreateStartService(
        InMemoryLiveTripSessionRepository repository)
    {
        return new LiveTripSessionService(
            repository,
            TimeProvider.System);
    }

    private static LiveTripSessionRecoveryService
        CreateRecoveryService(
            InMemoryLiveTripSessionRepository repository)
    {
        return new LiveTripSessionRecoveryService(
            repository,
            new FakeAccessChecker());
    }

    [Fact]
    public async Task Reconnect_Should_Return_Same_Active_Session()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var startService = CreateStartService(repository);

        var original = await startService.StartAsync(
            "plan-001");

        // Simulates a new client/service interaction.
        var recoveryService =
            CreateRecoveryService(repository);

        var recovered = await recoveryService.RecoverAsync(
            original.SessionId,
            "user-001");

        Assert.NotNull(recovered);
        Assert.Equal(original.SessionId, recovered.SessionId);
        Assert.Equal(original.TripPlanId, recovered.TripPlanId);
        Assert.Equal(LiveTripStatus.Active, recovered.Status);
        Assert.Equal(original.StartedAtUtc, recovered.StartedAtUtc);
    }

    [Fact]
    public async Task Reconnect_Should_Preserve_Paused_Status()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var startService = CreateStartService(repository);

        var original = await startService.StartAsync(
            "plan-001");

        await startService.PauseAsync(original.SessionId);

        var recoveryService =
            CreateRecoveryService(repository);

        var recovered = await recoveryService.RecoverAsync(
            original.SessionId,
            "user-001");

        Assert.NotNull(recovered);
        Assert.Equal(original.SessionId, recovered.SessionId);
        Assert.Equal(LiveTripStatus.Paused, recovered.Status);
        Assert.Equal(LiveTripStatus.Paused, original.Status);
    }

    [Fact]
    public async Task Finished_Session_Should_Not_Be_Recovered()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var startService = CreateStartService(repository);

        var original = await startService.StartAsync(
            "plan-001");

        await startService.FinishAsync(original.SessionId);

        var recoveryService =
            CreateRecoveryService(repository);

        var recovered = await recoveryService.RecoverAsync(
            original.SessionId,
            "user-001");

        Assert.Null(recovered);
        Assert.Equal(LiveTripStatus.Finished, original.Status);
    }

    [Fact]
    public async Task Unauthorized_User_Cannot_Reconnect()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var startService = CreateStartService(repository);

        var original = await startService.StartAsync(
            "plan-001");

        var recoveryService =
            CreateRecoveryService(repository);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => recoveryService.RecoverAsync(
                original.SessionId,
                "user-002"));
    }

    [Fact]
    public async Task Missing_Authentication_Should_Block_Reconnect()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var startService = CreateStartService(repository);

        var original = await startService.StartAsync(
            "plan-001");

        var recoveryService =
            CreateRecoveryService(repository);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => recoveryService.RecoverAsync(
                original.SessionId,
                "  "));
    }

    [Fact]
    public async Task Unknown_Session_Should_Return_Null()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var recoveryService =
            CreateRecoveryService(repository);

        var recovered = await recoveryService.RecoverAsync(
            Guid.NewGuid(),
            "user-001");

        Assert.Null(recovered);
    }

    [Fact]
    public async Task Repeated_Reconnect_Should_Not_Create_Session()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var startService = CreateStartService(repository);

        var original = await startService.StartAsync(
            "plan-001");

        var recoveryService =
            CreateRecoveryService(repository);

        var first = await recoveryService.RecoverAsync(
            original.SessionId,
            "user-001");

        var second = await recoveryService.RecoverAsync(
            original.SessionId,
            "user-001");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(original.SessionId, second.SessionId);

        var stored = await repository.GetByIdAsync(
            original.SessionId);

        Assert.Same(original, stored);
    }

    [Fact]
    public async Task New_Service_Instance_Should_Reuse_Open_Session()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var firstService = CreateStartService(repository);

        var original = await firstService.StartAsync(
            "plan-001");

        // A different service instance uses the
        // same repository, simulating reconnect.
        var secondService = CreateStartService(repository);

        var repeated = await secondService.StartAsync(
            "plan-001");

        Assert.Equal(original.SessionId, repeated.SessionId);
        Assert.Same(original, repeated);
    }
}
