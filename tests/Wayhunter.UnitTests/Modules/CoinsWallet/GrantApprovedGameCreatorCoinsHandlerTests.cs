
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class GrantApprovedGameCreatorCoinsHandlerTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ApprovedAt =
        SubmittedAt.AddHours(1);

    private static Game Draft(string creator = "creator-001")
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            creator,
            "Jaffna Treasure Hunt",
            "Find hidden places",
            60);

        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        var hint = new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Find the first location",
            null);

        game.ReplaceDraftStages(
            creator,
            new[] { step },
            new[] { hint });

        return game;
    }

    private static Game Pending(string creator = "creator-001")
    {
        var game = Draft(creator);

        game.SubmitForAdminReview(
            creator,
            SubmittedAt);

        return game;
    }

    private static Game Published(string creator = "creator-001")
    {
        var game = Pending(creator);
        game.ApproveCreation(ApprovedAt);
        return game;
    }

    private static GameCreatorApprovedRewardSnapshot Snapshot(
        Game game,
        string approval = "approval-001")
    {
        return new GameCreatorApprovedRewardSnapshot(
            game,
            approval);
    }

    private static GrantApprovedGameCreatorCoinsHandler Handler(
        FakeReader reader,
        InMemoryCoinWalletPrototypeStore store)
    {
        return new GrantApprovedGameCreatorCoinsHandler(
            reader,
            store,
            "v1");
    }

    [Fact]
    public async Task ApprovedPublishedGame_Credits100()
    {
        var game = Published();
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(Snapshot(game)), store);

        Assert.True(await handler.HandleAsync(
            game.Id, Guid.NewGuid()));

        var wallet = await store.GetByOwnerUserIdAsync(
            game.CreatorUserId);

        Assert.NotNull(wallet);
        Assert.Equal(100, wallet.Balance.PostedCoins);
        Assert.Equal(100, wallet.Balance.SpendableCoins);
        Assert.Equal(100, wallet.Balance.LifetimeEarnedCoins);
    }

    [Fact]
    public async Task DuplicateApproval_DoesNotDoubleCredit()
    {
        var game = Published();
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(Snapshot(game)), store);

        Assert.True(await handler.HandleAsync(
            game.Id, Guid.NewGuid()));

        Assert.False(await handler.HandleAsync(
            game.Id, Guid.NewGuid()));

        var wallet = await store.GetByOwnerUserIdAsync(
            game.CreatorUserId);

        Assert.NotNull(wallet);
        Assert.Equal(100, wallet.Balance.PostedCoins);
    }

    [Fact]
    public async Task ConcurrentApprovals_CreditOnlyOnce()
    {
        var game = Published();
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(Snapshot(game)), store);

        var calls = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
                handler.HandleAsync(
                    game.Id, Guid.NewGuid())));

        bool[] results = await Task.WhenAll(calls);

        Assert.Equal(1, results.Count(x => x));

        var wallet = await store.GetByOwnerUserIdAsync(
            game.CreatorUserId);

        Assert.NotNull(wallet);
        Assert.Equal(100, wallet.Balance.PostedCoins);
    }

    [Fact]
    public async Task DifferentApprovedGames_CreditSeparately()
    {
        var game1 = Published();
        var game2 = Published();

        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(
                Snapshot(game1),
                Snapshot(game2)),
            store);

        Assert.True(await handler.HandleAsync(
            game1.Id, Guid.NewGuid()));

        Assert.True(await handler.HandleAsync(
            game2.Id, Guid.NewGuid()));

        var wallet = await store.GetByOwnerUserIdAsync(
            game1.CreatorUserId);

        Assert.NotNull(wallet);
        Assert.Equal(200, wallet.Balance.PostedCoins);
    }

    [Fact]
    public async Task DifferentCreators_HaveSeparateWallets()
    {
        var first = Published("creator-001");
        var second = Published("creator-002");

        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(
                Snapshot(first),
                Snapshot(second)),
            store);

        Assert.True(await handler.HandleAsync(
            first.Id, Guid.NewGuid()));

        Assert.True(await handler.HandleAsync(
            second.Id, Guid.NewGuid()));

        var firstWallet = await store.GetByOwnerUserIdAsync(
            first.CreatorUserId);

        var secondWallet = await store.GetByOwnerUserIdAsync(
            second.CreatorUserId);

        Assert.NotNull(firstWallet);
        Assert.NotNull(secondWallet);

        Assert.Equal(100, firstWallet.Balance.PostedCoins);
        Assert.Equal(100, secondWallet.Balance.PostedCoins);
    }

    [Fact]
    public async Task DraftGame_DoesNotCredit()
    {
        var game = Draft();
        await AssertNoCredit(game);
    }

    [Fact]
    public async Task PendingGame_DoesNotCredit()
    {
        var game = Pending();
        await AssertNoCredit(game);
    }

    [Fact]
    public async Task RejectedGame_DoesNotCredit()
    {
        var game = Pending();
        game.RejectCreation();
        await AssertNoCredit(game);
    }

    [Fact]
    public async Task ExpiredGame_DoesNotCreateNewCredit()
    {
        var game = Published();

        Assert.True(game.ArchiveIfExpired(
            ApprovedAt.AddDays(30)));

        await AssertNoCredit(game);
    }

    private static async Task AssertNoCredit(Game game)
    {
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(Snapshot(game)), store);

        Assert.False(await handler.HandleAsync(
            game.Id, Guid.NewGuid()));

        Assert.Null(await store.GetByOwnerUserIdAsync(
            game.CreatorUserId));
    }

    [Fact]
    public async Task MissingGame_DoesNotCredit()
    {
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(new FakeReader(), store);

        Assert.False(await handler.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task MismatchedGameReference_IsRejected()
    {
        var game = Published();
        var another = Published();

        var reader = new FakeReader(Snapshot(game))
        {
            ForcedSnapshot = Snapshot(another)
        };

        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(reader, store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                game.Id, Guid.NewGuid()));

        Assert.Null(await store.GetByOwnerUserIdAsync(
            another.CreatorUserId));
    }

    [Fact]
    public async Task MissingApprovalRecord_IsRejected()
    {
        var game = Published();
        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(Snapshot(game, " ")),
            store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                game.Id, Guid.NewGuid()));

        Assert.Null(await store.GetByOwnerUserIdAsync(
            game.CreatorUserId));
    }

    [Fact]
    public async Task InvalidIdentifiers_AreRejected()
    {
        var game = Published();
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(Snapshot(game)), store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                Guid.Empty, Guid.NewGuid()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                game.Id, Guid.Empty));
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCredit()
    {
        var game = Published();
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(Snapshot(game)), store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                game.Id, Guid.NewGuid(), cts.Token));

        Assert.Null(await store.GetByOwnerUserIdAsync(
            game.CreatorUserId));
    }

    [Fact]
    public async Task LegacyGameWithoutSubmission_IsRejected()
    {
        var game = new Game(
            Guid.NewGuid(), "creator-001");

        game.ApproveCreation(ApprovedAt);

        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(Snapshot(game)), store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                game.Id, Guid.NewGuid()));

        Assert.Null(await store.GetByOwnerUserIdAsync(
            game.CreatorUserId));
    }

    private sealed class FakeReader :
        IGameCreatorApprovedRewardReader
    {
        private readonly Dictionary<Guid,
            GameCreatorApprovedRewardSnapshot> _items = new();

        public GameCreatorApprovedRewardSnapshot?
            ForcedSnapshot
        { get; set; }

        public FakeReader(
            params GameCreatorApprovedRewardSnapshot[] items)
        {
            foreach (var item in items)
            {
                _items.Add(item.Game.Id, item);
            }
        }

        public Task<GameCreatorApprovedRewardSnapshot?>
            FindByGameIdAsync(
                Guid gameId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ForcedSnapshot is not null)
                return Task.FromResult(
                    ForcedSnapshot);

            _items.TryGetValue(gameId, out var snapshot);

            return Task.FromResult(snapshot);
        }
    }
}
