
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GetPendingGameStageProofsServiceTests
{
    [Fact]
    public async Task GetAsync_ShouldReturnPendingProofs()
    {
        var expected = new List<GamePendingStageProofDto>
        {
            new(
                Guid.NewGuid(),
                "Jaffna Treasure Hunt",
                "player-001",
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "camera-photo-001",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(25))
        };

        var store = new FakePendingProofQueryStore(expected);
        var service = new GetPendingGameStageProofsService(store);

        var result = await service.GetAsync();

        Assert.Single(result);
        Assert.Equal(expected[0].EvidenceId, result[0].EvidenceId);
        Assert.Equal(1, store.CallCount);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnEmptyWhenQueueIsEmpty()
    {
        var store = new FakePendingProofQueryStore(
            Array.Empty<GamePendingStageProofDto>());

        var service = new GetPendingGameStageProofsService(store);

        var result = await service.GetAsync();

        Assert.Empty(result);
        Assert.Equal(1, store.CallCount);
    }

    [Fact]
    public async Task GetAsync_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        var store = new FakePendingProofQueryStore(
            Array.Empty<GamePendingStageProofDto>());

        var service = new GetPendingGameStageProofsService(store);

        await service.GetAsync(cts.Token);

        Assert.Equal(cts.Token, store.ReceivedToken);
    }

    [Fact]
    public async Task GetAsync_WithCancelledToken_ShouldNotCallStore()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var store = new FakePendingProofQueryStore(
            Array.Empty<GamePendingStageProofDto>());

        var service = new GetPendingGameStageProofsService(store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetAsync(cts.Token));

        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public void Constructor_WithNullStore_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GetPendingGameStageProofsService(null!));
    }

    private sealed class FakePendingProofQueryStore
        : IGamePendingStageProofQueryStore
    {
        private readonly IReadOnlyList<GamePendingStageProofDto> _items;

        public int CallCount { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public FakePendingProofQueryStore(
            IReadOnlyList<GamePendingStageProofDto> items)
        {
            _items = items;
        }

        public Task<IReadOnlyList<GamePendingStageProofDto>>
            GetPendingStageProofsAsync(
                CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedToken = cancellationToken;

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_items);
        }
    }
}
