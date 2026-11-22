using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GamesReadQueryServicesTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AvailableGames_UsesServerUtcClock()
    {
        var store = new AvailableStore();
        var service = new GetAvailableGamesQueryService(store, new FixedTimeProvider(Now));

        var result = await service.ExecuteAsync();

        Assert.Equal(Now, store.ReceivedNowUtc);
        Assert.Empty(result);
    }

    [Fact]
    public async Task PublicDetails_ReturnsNullForInaccessibleGame()
    {
        var service = new GetPublicGameDetailsQueryService(
            new DetailsStore(), new FixedTimeProvider(Now));

        Assert.Null(await service.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Attempts_UsesOnlyTheResolvedPlayerId()
    {
        var store = new AttemptsStore();
        var service = new GetMyGameAttemptsQueryService(store);

        var result = await service.ExecuteAsync("trusted-account-id");

        Assert.Equal("trusted-account-id", store.ReceivedUserId);
        Assert.Single(result);
        Assert.Equal(GameProgressStatus.InProgress, result[0].Status);
    }

    [Fact]
    public async Task PersistenceFailure_IsClassifiedAsUnavailable()
    {
        var service = new GetAvailableGamesQueryService(
            new FailedAvailableStore(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<GamesReadDependencyUnavailableException>(
            () => service.ExecuteAsync());
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }

    private sealed class AvailableStore : IAvailableGamesQueryStore
    {
        public DateTimeOffset? ReceivedNowUtc { get; private set; }

        public Task<IReadOnlyList<PublicGameSummaryDto>> GetAvailableGamesAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            ReceivedNowUtc = nowUtc;
            return Task.FromResult<IReadOnlyList<PublicGameSummaryDto>>([]);
        }
    }

    private sealed class FailedAvailableStore : IAvailableGamesQueryStore
    {
        public Task<IReadOnlyList<PublicGameSummaryDto>> GetAvailableGamesAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<PublicGameSummaryDto>>(
                new InvalidOperationException("Storage unavailable."));
    }

    private sealed class DetailsStore : IPublicGameDetailsQueryStore
    {
        public Task<PublicGameDetailsDto?> GetPublicGameAsync(
            Guid gameId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PublicGameDetailsDto?>(null);
    }

    private sealed class AttemptsStore : IMyGameAttemptsQueryStore
    {
        public string? ReceivedUserId { get; private set; }

        public Task<IReadOnlyList<MyGameAttemptDto>> GetMyAttemptsAsync(
            string playerUserId,
            CancellationToken cancellationToken = default)
        {
            ReceivedUserId = playerUserId;
            return Task.FromResult<IReadOnlyList<MyGameAttemptDto>>(
            [new(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddHours(1),
                GameProgressStatus.InProgress, 1, 2)]);
        }
    }
}
