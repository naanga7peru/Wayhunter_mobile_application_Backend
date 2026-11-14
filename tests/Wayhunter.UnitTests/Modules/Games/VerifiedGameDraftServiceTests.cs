
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class VerifiedGameDraftServiceTests
{
    private const string CreatorId = "creator-001";

    // =====================================
    // FAKE REPOSITORY - NO SQL DATABASE
    // =====================================

    private sealed class FakeGameRepository : IGameRepository
    {
        private readonly Dictionary<Guid, Game> _games = new();

        public int AddCount { get; private set; }
        public int UpdateCount { get; private set; }

        public Task<Game?> GetByIdAsync(
            Guid gameId,
            CancellationToken cancellationToken = default)
        {
            _games.TryGetValue(gameId, out var game);

            return Task.FromResult<Game?>(game);
        }

        public Task AddAsync(
            Game game,
            CancellationToken cancellationToken = default)
        {
            _games.Add(game.Id, game);
            AddCount++;

            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Game game,
            CancellationToken cancellationToken = default)
        {
            _games[game.Id] = game;
            UpdateCount++;

            return Task.CompletedTask;
        }
    }

    // =====================================
    // FAKE MEDIA ACCESS VERIFIER
    // =====================================

    private sealed class FakeMediaVerifier
        : IGameClueMediaAccessVerifier
    {
        private readonly HashSet<string> _allowed =
            new(StringComparer.Ordinal);

        public int CallCount { get; private set; }

        public void Allow(string mediaId)
        {
            _allowed.Add(mediaId);
        }

        public Task<bool> IsUsableCreatorClueMediaAsync(
            string mediaAssetId,
            string creatorUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            return Task.FromResult(
                creatorUserId == CreatorId &&
                _allowed.Contains(mediaAssetId));
        }
    }

    // =====================================
    // HELPER - VALID DRAFT
    // =====================================

    private static CreateGameDraftDto NewDraft(
        string? textHint,
        string? photoMediaId)
    {
        return new CreateGameDraftDto(
            "Jaffna Treasure Hunt",
            "Game draft for verification",
            60,
            new[]
            {
                new GameDraftStageDto(
                    1,
                    textHint,
                    photoMediaId)
            });
    }

    private static VerifiedGameDraftService NewService(
        FakeGameRepository repository,
        FakeMediaVerifier verifier)
    {
        var gameService = new GameService(repository);

        var mediaService =
            new GameClueMediaOwnershipService(verifier);

        return new VerifiedGameDraftService(
            gameService,
            mediaService);
    }

    // =====================================
    // TEST 01 - AUTHORIZED CREATE
    // =====================================

    [Fact]
    public async Task AuthorizedPhoto_ShouldCreateDraft()
    {
        var repository = new FakeGameRepository();
        var verifier = new FakeMediaVerifier();

        verifier.Allow("owned-photo-001");

        var service = NewService(repository, verifier);

        var draft = NewDraft(
            null,
            "owned-photo-001");

        var game = await service.CreateDraftAsync(
            CreatorId,
            draft);

        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal(CreatorId, game.CreatorUserId);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(1, verifier.CallCount);

        var saved = await repository.GetByIdAsync(game.Id);

        Assert.Same(game, saved);
    }

    // =====================================
    // TEST 02 - UNAUTHORIZED CREATE
    // =====================================

    [Theory]
    [InlineData("another-users-photo")]
    [InlineData("private-player-proof")]
    [InlineData("unknown-photo")]
    public async Task UnauthorizedPhoto_ShouldNotCreateDraft(
        string mediaId)
    {
        var repository = new FakeGameRepository();
        var verifier = new FakeMediaVerifier();

        var service = NewService(repository, verifier);

        var draft = NewDraft(null, mediaId);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => service.CreateDraftAsync(
                CreatorId,
                draft));

        Assert.Equal(0, repository.AddCount);
        Assert.Equal(1, verifier.CallCount);
    }

    // =====================================
    // TEST 03 - TEXT ONLY CREATE
    // =====================================

    [Fact]
    public async Task TextOnlyDraft_ShouldSkipMediaVerifier()
    {
        var repository = new FakeGameRepository();
        var verifier = new FakeMediaVerifier();

        var service = NewService(repository, verifier);

        var draft = NewDraft(
            "Find the hidden location",
            null);

        var game = await service.CreateDraftAsync(
            CreatorId,
            draft);

        Assert.NotNull(game);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(0, verifier.CallCount);
    }

    // =====================================
    // TEST 04 - INVALID CREATE
    // =====================================

    [Fact]
    public async Task InvalidDraft_ShouldNotBeSaved()
    {
        var repository = new FakeGameRepository();
        var verifier = new FakeMediaVerifier();

        var service = NewService(repository, verifier);

        var draft = NewDraft(
            "First clue",
            null) with
        {
            Title = " "
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateDraftAsync(
                CreatorId,
                draft));

        Assert.Equal(0, repository.AddCount);
        Assert.Equal(0, verifier.CallCount);
    }

    // =====================================
    // TEST 05 - AUTHORIZED UPDATE
    // =====================================

    [Fact]
    public async Task AuthorizedPhoto_ShouldUpdateDraft()
    {
        var repository = new FakeGameRepository();
        var verifier = new FakeMediaVerifier();

        var service = NewService(repository, verifier);

        var original = await service.CreateDraftAsync(
            CreatorId,
            NewDraft("Original clue", null));

        verifier.Allow("owned-photo-002");

        var updatedRequest = NewDraft(
            "Updated clue",
            "owned-photo-002") with
        {
            Title = "Updated Jaffna Game",
            DurationMinutes = 90
        };

        var updated = await service.UpdateDraftAsync(
            original.Id,
            CreatorId,
            updatedRequest);

        Assert.Equal(
            "Updated Jaffna Game",
            updated.Title);

        Assert.Equal(90, updated.DurationMinutes);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(1, verifier.CallCount);
    }

    // =====================================
    // TEST 06 - UNAUTHORIZED UPDATE
    // =====================================

    [Fact]
    public async Task UnauthorizedPhoto_ShouldPreserveOldDraft()
    {
        var repository = new FakeGameRepository();
        var verifier = new FakeMediaVerifier();

        var service = NewService(repository, verifier);

        var original = await service.CreateDraftAsync(
            CreatorId,
            NewDraft("Original clue", null));

        var originalStepId = original.DraftSteps[0].Id;

        var updatedRequest = NewDraft(
            null,
            "unauthorized-photo") with
        {
            Title = "Changed Title",
            DurationMinutes = 120
        };

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => service.UpdateDraftAsync(
                original.Id,
                CreatorId,
                updatedRequest));

        Assert.Equal(
            "Jaffna Treasure Hunt",
            original.Title);

        Assert.Equal(60, original.DurationMinutes);

        Assert.Equal(
            originalStepId,
            original.DraftSteps[0].Id);

        Assert.Equal(0, repository.UpdateCount);
    }

    // =====================================
    // TEST 07 - ANOTHER USER UPDATE
    // =====================================

    [Fact]
    public async Task AnotherUser_ShouldNotUpdateCreatorsDraft()
    {
        var repository = new FakeGameRepository();
        var verifier = new FakeMediaVerifier();

        var service = NewService(repository, verifier);

        var game = await service.CreateDraftAsync(
            CreatorId,
            NewDraft("Original clue", null));

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => service.UpdateDraftAsync(
                game.Id,
                "another-user",
                NewDraft(null, "photo-001")));

        Assert.Equal(0, repository.UpdateCount);

        // Owner check happens before media check.
        Assert.Equal(0, verifier.CallCount);
    }

    // =====================================
    // TEST 08 - PENDING GAME UPDATE
    // =====================================

    [Fact]
    public async Task PendingGame_ShouldNotBeUpdatedAsDraft()
    {
        var repository = new FakeGameRepository();
        var verifier = new FakeMediaVerifier();

        var gameService = new GameService(repository);

        var mediaService =
            new GameClueMediaOwnershipService(verifier);

        var service = new VerifiedGameDraftService(
            gameService,
            mediaService);

        var pendingGame =
            await gameService.CreatePendingGameAsync(
                CreatorId);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => service.UpdateDraftAsync(
                pendingGame.Id,
                CreatorId,
                NewDraft(null, "photo-001")));

        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(0, verifier.CallCount);
    }

    // =====================================
    // TEST 09 - MISSING GAME SERVICE
    // =====================================

    [Fact]
    public void NullGameService_ShouldBeRejected()
    {
        var verifier = new FakeMediaVerifier();

        var mediaService =
            new GameClueMediaOwnershipService(verifier);

        Assert.Throws<ArgumentNullException>(() =>
            new VerifiedGameDraftService(
                null!,
                mediaService));
    }

    // =====================================
    // TEST 10 - MISSING MEDIA SERVICE
    // =====================================

    [Fact]
    public void NullMediaService_ShouldBeRejected()
    {
        var repository = new FakeGameRepository();

        var gameService = new GameService(repository);

        Assert.Throws<ArgumentNullException>(() =>
            new VerifiedGameDraftService(
                gameService,
                null!));
    }
}
