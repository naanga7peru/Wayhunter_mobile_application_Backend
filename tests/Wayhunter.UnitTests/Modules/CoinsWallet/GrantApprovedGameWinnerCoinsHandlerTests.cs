
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class GrantApprovedGameWinnerCoinsHandlerTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset PublishedAt =
        SubmittedAt.AddHours(1);

    private static readonly DateTimeOffset VerifiedAt =
        PublishedAt.AddMinutes(15);

    private static Game PublishedGame(
        string creatorId = "creator-001")
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            creatorId,
            "Jaffna Treasure Hunt",
            "Find the hidden location",
            60);

        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        var hint = new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Find the first place",
            null);

        game.ReplaceDraftStages(
            creatorId,
            new[] { step },
            new[] { hint });

        game.SubmitForAdminReview(
            creatorId,
            SubmittedAt);

        game.ApproveCreation(PublishedAt);

        return game;
    }

    private static GameWinnerApprovedRewardSnapshot Approved(
        string player = "player-001",
        string creator = "creator-001")
    {
        var game = PublishedGame(creator);

        var attempt = new GameAttempt(
            Guid.NewGuid(),
            game.Id,
            player,
            PublishedAt.AddMinutes(5));

        game.ApproveVerifiedWinner(
            player,
            VerifiedAt);

        return new GameWinnerApprovedRewardSnapshot(
            Game: game,
            Attempt: attempt,
            ReviewOutcome:
                GameWinnerReviewOutcome.Approved,
            ApprovalRecordId:
                Guid.NewGuid().ToString("D"),
            ReviewerAdminUserId: "admin-001",
            VerifiedAtUtc: VerifiedAt);
    }

    private static GrantApprovedGameWinnerCoinsHandler Handler(
        FakeReader reader,
        ICoinRewardGrantStore store)
    {
        return new GrantApprovedGameWinnerCoinsHandler(
            reader,
            store,
            "v1");
    }

    [Fact]
    public async Task ApprovedWinner_Gets100Coins()
    {
        var snapshot = Approved();
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(snapshot), store);

        Assert.True(await handler.HandleAsync(
            snapshot.Game.Id,
            Guid.NewGuid()));

        var wallet = await store.GetByOwnerUserIdAsync(
            snapshot.Attempt.PlayerUserId);

        Assert.NotNull(wallet);

        Assert.Equal(100, wallet.Balance.PostedCoins);
        Assert.Equal(100, wallet.Balance.SpendableCoins);
        Assert.Equal(100, wallet.Balance.LifetimeEarnedCoins);
    }

    [Fact]
    public async Task DuplicateWinnerEvent_CreditsOnlyOnce()
    {
        var snapshot = Approved();
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(snapshot), store);

        Assert.True(await handler.HandleAsync(
            snapshot.Game.Id,
            Guid.NewGuid()));

        Assert.False(await handler.HandleAsync(
            snapshot.Game.Id,
            Guid.NewGuid()));

        var wallet = await store.GetByOwnerUserIdAsync(
            snapshot.Attempt.PlayerUserId);

        Assert.NotNull(wallet);

        Assert.Equal(100, wallet.Balance.PostedCoins);
    }

    [Fact]
    public async Task ConcurrentDuplicateEvents_CreditOnce()
    {
        var snapshot = Approved();
        var store = new InMemoryCoinWalletPrototypeStore();
        var handler = Handler(
            new FakeReader(snapshot), store);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
                handler.HandleAsync(
                    snapshot.Game.Id,
                    Guid.NewGuid())));

        bool[] results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(x => x));

        var wallet = await store.GetByOwnerUserIdAsync(
            snapshot.Attempt.PlayerUserId);

        Assert.NotNull(wallet);

        Assert.Equal(100, wallet.Balance.PostedCoins);
    }

    [Fact]
    public async Task DifferentGames_RewardSeparately()
    {
        var first = Approved();
        var second = Approved();

        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(first, second),
            store);

        Assert.True(await handler.HandleAsync(
            first.Game.Id,
            Guid.NewGuid()));

        Assert.True(await handler.HandleAsync(
            second.Game.Id,
            Guid.NewGuid()));

        var wallet = await store.GetByOwnerUserIdAsync(
            first.Attempt.PlayerUserId);

        Assert.NotNull(wallet);

        Assert.Equal(200, wallet.Balance.PostedCoins);
    }

    [Fact]
    public async Task DifferentWinners_HaveSeparateWallets()
    {
        var first = Approved("player-001");
        var second = Approved("player-002");

        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(first, second),
            store);

        Assert.True(await handler.HandleAsync(
            first.Game.Id,
            Guid.NewGuid()));

        Assert.True(await handler.HandleAsync(
            second.Game.Id,
            Guid.NewGuid()));

        var firstWallet =
            await store.GetByOwnerUserIdAsync(
                "player-001");

        var secondWallet =
            await store.GetByOwnerUserIdAsync(
                "player-002");

        Assert.NotNull(firstWallet);
        Assert.NotNull(secondWallet);

        Assert.Equal(100, firstWallet.Balance.PostedCoins);
        Assert.Equal(100, secondWallet.Balance.PostedCoins);
    }

    [Theory]
    [InlineData(GameWinnerReviewOutcome.Pending)]
    [InlineData(GameWinnerReviewOutcome.Rejected)]
    public async Task UnapprovedResult_GetsZero(
        GameWinnerReviewOutcome outcome)
    {
        var snapshot = Approved() with
        {
            ReviewOutcome = outcome
        };

        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(snapshot),
            store);

        Assert.False(await handler.HandleAsync(
            snapshot.Game.Id,
            Guid.NewGuid()));

        Assert.Null(await store.GetByOwnerUserIdAsync(
            snapshot.Attempt.PlayerUserId));
    }

    [Fact]
    public async Task NoRecordedWinner_GetsZero()
    {
        var game = PublishedGame();

        var attempt = new GameAttempt(
            Guid.NewGuid(),
            game.Id,
            "player-001",
            PublishedAt.AddMinutes(5));

        var snapshot =
            new GameWinnerApprovedRewardSnapshot(
                game,
                attempt,
                GameWinnerReviewOutcome.Approved,
                "approval-001",
                "admin-001",
                VerifiedAt);

        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(snapshot),
            store);

        Assert.False(await handler.HandleAsync(
            game.Id,
            Guid.NewGuid()));
    }

    [Fact]
    public void Creator_CannotWinOwnGame()
    {
        var game = PublishedGame("creator-001");

        Assert.Throws<InvalidOperationException>(() =>
            game.ApproveVerifiedWinner(
                "creator-001",
                VerifiedAt));
    }

    [Fact]
    public void GameExpiredBeforeVerification_CannotWin()
    {
        var game = PublishedGame();

        Assert.Throws<InvalidOperationException>(() =>
            game.ApproveVerifiedWinner(
                "player-001",
                PublishedAt.AddDays(30)));
    }

    [Fact]
    public async Task ArchivedPreviouslyVerifiedWinner_IsRetryable()
    {
        var snapshot = Approved();

        Assert.True(snapshot.Game.ArchiveIfExpired(
            PublishedAt.AddDays(30)));

        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(snapshot),
            store);

        Assert.True(await handler.HandleAsync(
            snapshot.Game.Id,
            Guid.NewGuid()));

        Assert.False(await handler.HandleAsync(
            snapshot.Game.Id,
            Guid.NewGuid()));
    }

    [Fact]
    public async Task FlaggedGame_DoesNotGrant()
    {
        var snapshot = Approved();

        snapshot.Game.FlagForReview(
            "Suspicious proof",
            VerifiedAt.AddMinutes(1));

        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(snapshot),
            store);

        Assert.False(await handler.HandleAsync(
            snapshot.Game.Id,
            Guid.NewGuid()));
    }

    [Fact]
    public async Task WrongGameReference_IsRejected()
    {
        var snapshot = Approved();

        var store = new InMemoryCoinWalletPrototypeStore();

        var reader = new FakeReader(snapshot)
        {
            ForcedSnapshot = snapshot
        };

        var handler = Handler(reader, store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                Guid.NewGuid(),
                Guid.NewGuid()));
    }

    [Fact]
    public async Task WrongAttemptGame_IsRejected()
    {
        var snapshot = Approved();

        var invalidAttempt = new GameAttempt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            snapshot.Attempt.PlayerUserId,
            snapshot.Attempt.StartedAtUtc);

        var invalid = snapshot with
        {
            Attempt = invalidAttempt
        };

        var handler = Handler(
            new FakeReader(invalid),
            new InMemoryCoinWalletPrototypeStore());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                snapshot.Game.Id,
                Guid.NewGuid()));
    }

    [Fact]
    public async Task WrongAttemptPlayer_IsRejected()
    {
        var snapshot = Approved();

        var invalidAttempt = new GameAttempt(
            Guid.NewGuid(),
            snapshot.Game.Id,
            "different-player",
            PublishedAt.AddMinutes(5));

        var invalid = snapshot with
        {
            Attempt = invalidAttempt
        };

        var handler = Handler(
            new FakeReader(invalid),
            new InMemoryCoinWalletPrototypeStore());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                snapshot.Game.Id,
                Guid.NewGuid()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingAdminApproval_IsRejected(
        bool missingApprovalId)
    {
        var snapshot = Approved();

        var invalid = missingApprovalId
            ? snapshot with { ApprovalRecordId = " " }
            : snapshot with { ReviewerAdminUserId = " " };

        var handler = Handler(
            new FakeReader(invalid),
            new InMemoryCoinWalletPrototypeStore());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                snapshot.Game.Id,
                Guid.NewGuid()));
    }

    [Fact]
    public async Task VerificationAfterExpiry_IsRejected()
    {
        var snapshot = Approved() with
        {
            VerifiedAtUtc = PublishedAt.AddDays(30)
        };

        var handler = Handler(
            new FakeReader(snapshot),
            new InMemoryCoinWalletPrototypeStore());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                snapshot.Game.Id,
                Guid.NewGuid()));
    }

    [Fact]
    public async Task NonUtcVerification_IsRejected()
    {
        var snapshot = Approved() with
        {
            VerifiedAtUtc = VerifiedAt.ToOffset(
                TimeSpan.FromHours(5.5))
        };

        var handler = Handler(
            new FakeReader(snapshot),
            new InMemoryCoinWalletPrototypeStore());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                snapshot.Game.Id,
                Guid.NewGuid()));
    }

    [Fact]
    public async Task VerificationBeforeAttempt_IsRejected()
    {
        var snapshot = Approved() with
        {
            VerifiedAtUtc =
                PublishedAt.AddMinutes(1)
        };

        var handler = Handler(
            new FakeReader(snapshot),
            new InMemoryCoinWalletPrototypeStore());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                snapshot.Game.Id,
                Guid.NewGuid()));
    }

    [Fact]
    public async Task MissingGame_ReturnsFalse()
    {
        var handler = Handler(
            new FakeReader(),
            new InMemoryCoinWalletPrototypeStore());

        Assert.False(await handler.HandleAsync(
            Guid.NewGuid(),
            Guid.NewGuid()));
    }

    [Fact]
    public async Task EmptyIdentifiers_AreRejected()
    {
        var snapshot = Approved();

        var handler = Handler(
            new FakeReader(snapshot),
            new InMemoryCoinWalletPrototypeStore());

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                Guid.Empty,
                Guid.NewGuid()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                snapshot.Game.Id,
                Guid.Empty));
    }

    [Fact]
    public async Task Cancellation_DoesNotGrant()
    {
        var snapshot = Approved();
        var store = new InMemoryCoinWalletPrototypeStore();

        var handler = Handler(
            new FakeReader(snapshot),
            store);

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                snapshot.Game.Id,
                Guid.NewGuid(),
                cts.Token));

        Assert.Null(await store.GetByOwnerUserIdAsync(
            snapshot.Attempt.PlayerUserId));
    }

    [Fact]
    public async Task RewardContainsGameAndAttemptReferences()
    {
        var snapshot = Approved();
        var store = new RecordingStore();

        var handler = Handler(
            new FakeReader(snapshot),
            store);

        Assert.True(await handler.HandleAsync(
            snapshot.Game.Id,
            Guid.NewGuid()));

        var reward = Assert.Single(store.Requests);

        Assert.Equal(
            CoinRewardType.GameWinnerApproved,
            reward.RewardType);

        Assert.Equal(100, reward.CoinsAmount);

        Assert.Equal(
            snapshot.Game.Id.ToString("D"),
            reward.SourceRecordId);

        Assert.Equal(
            snapshot.Attempt.Id.ToString("D"),
            reward.GameAttemptId);

        Assert.Equal(
            snapshot.ApprovalRecordId,
            reward.ApprovalRecordId);

        Assert.Equal(
            snapshot.Attempt.PlayerUserId,
            reward.RecipientUserId);

        Assert.Equal(
            VerifiedAt,
            reward.EligibilityConfirmedAtUtc);
    }

    private sealed class FakeReader :
        IGameWinnerApprovedRewardReader
    {
        private readonly Dictionary<
            Guid, GameWinnerApprovedRewardSnapshot>
            _items = new();

        public GameWinnerApprovedRewardSnapshot?
            ForcedSnapshot
        { get; set; }

        public FakeReader(
            params GameWinnerApprovedRewardSnapshot[] snapshots)
        {
            foreach (var item in snapshots)
            {
                _items[item.Game.Id] = item;
            }
        }

        public Task<GameWinnerApprovedRewardSnapshot?>
            FindByGameIdAsync(
                Guid gameId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ForcedSnapshot is not null)
            {
                return Task.FromResult<
                    GameWinnerApprovedRewardSnapshot?>(
                        ForcedSnapshot);
            }

            _items.TryGetValue(
                gameId,
                out var snapshot);

            return Task.FromResult<
                GameWinnerApprovedRewardSnapshot?>(
                    snapshot);
        }
    }

    private sealed class RecordingStore :
        ICoinRewardGrantStore
    {
        private readonly List<CoinRewardGrantRequest>
            _requests = new();

        public IReadOnlyList<CoinRewardGrantRequest> Requests =>
            _requests;

        public Task<bool> TryGrantOnceAsync(
            CoinRewardGrantRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _requests.Add(request);

            return Task.FromResult(true);
        }
    }
}
