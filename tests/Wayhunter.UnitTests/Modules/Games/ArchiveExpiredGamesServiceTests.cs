using System;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class ArchiveExpiredGamesServiceTests
{
    private static readonly DateTimeOffset ApprovedAtUtc =
        new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ArchiveBatch_ExpiresAndRetainsPublishedGame_Idempotently()
    {
        var nowUtc = ApprovedAtUtc.AddDays(30);
        var game = new Game(Guid.NewGuid(), "creator-user");
        game.ApproveCreation(ApprovedAtUtc);

        var store = new InMemoryGameSubmissionStore();
        await store.AddAsync(game);
        var service = new ArchiveExpiredGamesService(
            store, new FixedTimeProvider(nowUtc));

        var archivedCount = await service.ArchiveBatchAsync(10);
        var retryCount = await service.ArchiveBatchAsync(10);
        var retained = await store.GetByIdAsync(game.Id);

        Assert.Equal(1, archivedCount);
        Assert.Equal(0, retryCount);
        Assert.NotNull(retained);
        Assert.Equal(GameStatus.Expired, retained.Status);
        Assert.Equal(ApprovedAtUtc.AddDays(30), retained.ExpiresAtUtc);
        Assert.False(retained.CanPlay(nowUtc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ArchiveBatch_RejectsNonPositiveBatchSize(int maxCount)
    {
        var service = new ArchiveExpiredGamesService(
            new InMemoryGameSubmissionStore(),
            new FixedTimeProvider(ApprovedAtUtc));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ArchiveBatchAsync(maxCount));
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }
}
