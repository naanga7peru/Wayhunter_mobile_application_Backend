using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class StartGameAttemptServiceTests
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 11, 20, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_UsesTrustedPlayerIdAndServerUtc()
    {
        var store = new RecordingStore();
        var service = new StartGameAttemptService(
            store,
            new FixedTimeProvider(NowUtc));
        var gameId = Guid.NewGuid();

        var result = await service.ExecuteAsync(gameId, "user-123");

        Assert.Equal(gameId, store.GameId);
        Assert.Equal("user-123", store.PlayerUserId);
        Assert.Equal(NowUtc, store.NowUtc);
        Assert.Same(store.Result, result);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToPersistence()
    {
        var store = new RecordingStore();
        var service = new StartGameAttemptService(store, new FixedTimeProvider(NowUtc));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExecuteAsync(Guid.NewGuid(), "user-123", source.Token));
        Assert.Equal(source.Token, store.CancellationToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_RejectsMissingPlayerWithoutPersistence(string playerId)
    {
        var store = new RecordingStore();
        var service = new StartGameAttemptService(store, new FixedTimeProvider(NowUtc));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExecuteAsync(Guid.NewGuid(), playerId));
        Assert.False(store.Called);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsMissingGameWithoutPersistence()
    {
        var store = new RecordingStore();
        var service = new StartGameAttemptService(store, new FixedTimeProvider(NowUtc));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExecuteAsync(Guid.Empty, "user-123"));
        Assert.False(store.Called);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotMaskPersistenceFailure()
    {
        var store = new RecordingStore { Failure = new InvalidOperationException("storage unavailable") };
        var service = new StartGameAttemptService(store, new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(Guid.NewGuid(), "user-123"));
        Assert.Equal("storage unavailable", error.Message);
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }

    private sealed class RecordingStore : IStartGameAttemptStore
    {
        public StartGameAttemptResultDto Result { get; } = new(
            Guid.NewGuid(), Guid.NewGuid(), "user-123", StartGameAttemptServiceTests.NowUtc,
            StartGameAttemptServiceTests.NowUtc.AddMinutes(30), 1, false);
        public Exception? Failure { get; init; }
        public bool Called { get; private set; }
        public Guid GameId { get; private set; }
        public string? PlayerUserId { get; private set; }
        public DateTimeOffset NowUtc { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<StartGameAttemptResultDto> StartOrResumeAsync(
            Guid gameId,
            string playerUserId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            GameId = gameId;
            PlayerUserId = playerUserId;
            NowUtc = nowUtc;
            CancellationToken = cancellationToken;
            if (Failure is not null)
                return Task.FromException<StartGameAttemptResultDto>(Failure);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result);
        }
    }
}
