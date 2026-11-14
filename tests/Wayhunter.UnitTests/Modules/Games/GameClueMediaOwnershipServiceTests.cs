
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameClueMediaOwnershipServiceTests
{
    private const string CreatorId = "creator-001";

    private static CreateGameDraftDto NewDraft(
        params GameDraftStageDto[] stages)
    {
        return new CreateGameDraftDto(
            "Jaffna Treasure Hunt",
            "Find the hidden places",
            60,
            stages);
    }

    // Fake verifier for unit testing only.
    // It does NOT access real Cloudinary or SQL data.
    private sealed class FakeMediaVerifier
        : IGameClueMediaAccessVerifier
    {
        private readonly HashSet<string> _allowedMedia =
            new(StringComparer.Ordinal);

        public List<(string MediaId, string CreatorId)> Calls
        { get; } = new();

        public void Allow(string mediaId)
        {
            _allowedMedia.Add(mediaId);
        }

        public Task<bool> IsUsableCreatorClueMediaAsync(
            string mediaAssetId,
            string creatorUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Calls.Add((mediaAssetId, creatorUserId));

            return Task.FromResult(
                _allowedMedia.Contains(mediaAssetId));
        }
    }

    // TEST 01 - Text only

    [Fact]
    public async Task TextOnlyClue_ShouldSkipMediaCheck()
    {
        var verifier = new FakeMediaVerifier();
        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, "Find the first location", null));

        await service.ValidateCreatorClueMediaAsync(
            CreatorId, draft);

        Assert.Empty(verifier.Calls);
    }

    // TEST 02 - Authorized creator photo

    [Fact]
    public async Task AuthorizedPhoto_ShouldBeAccepted()
    {
        var verifier = new FakeMediaVerifier();
        verifier.Allow("photo-owned-001");

        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, null, "photo-owned-001"));

        await service.ValidateCreatorClueMediaAsync(
            CreatorId, draft);

        var call = Assert.Single(verifier.Calls);

        Assert.Equal("photo-owned-001", call.MediaId);
        Assert.Equal(CreatorId, call.CreatorId);
    }

    // TEST 03 - Disallowed media
    // The fake simulates each denied media reference.

    [Theory]
    [InlineData("another-users-photo")]
    [InlineData("private-player-proof")]
    [InlineData("unknown-media-id")]
    public async Task DisallowedMedia_ShouldBeRejected(
        string mediaId)
    {
        var verifier = new FakeMediaVerifier();
        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, null, mediaId));

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => service.ValidateCreatorClueMediaAsync(
                CreatorId, draft));

        Assert.Single(verifier.Calls);
    }

    // TEST 04 - Duplicate photo

    [Fact]
    public async Task DuplicateMedia_ShouldBeCheckedOnce()
    {
        var verifier = new FakeMediaVerifier();
        verifier.Allow("photo-001");

        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, null, "photo-001"),
            new GameDraftStageDto(
                2, "Second clue", "photo-001"));

        await service.ValidateCreatorClueMediaAsync(
            CreatorId, draft);

        Assert.Single(verifier.Calls);
    }

    // TEST 05 - Different photos

    [Fact]
    public async Task DifferentPhotos_ShouldAllBeChecked()
    {
        var verifier = new FakeMediaVerifier();

        verifier.Allow("photo-001");
        verifier.Allow("photo-002");

        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, null, "photo-001"),
            new GameDraftStageDto(
                2, null, "photo-002"));

        await service.ValidateCreatorClueMediaAsync(
            CreatorId, draft);

        Assert.Equal(2, verifier.Calls.Count);
    }

    // TEST 06 - Blank creator ID

    [Fact]
    public async Task EmptyCreatorId_ShouldBeRejected()
    {
        var verifier = new FakeMediaVerifier();
        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, "Text hint", null));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ValidateCreatorClueMediaAsync(
                " ", draft));

        Assert.Empty(verifier.Calls);
    }

    // TEST 07 - Invalid draft

    [Fact]
    public async Task InvalidDraft_ShouldRejectBeforeMediaCheck()
    {
        var verifier = new FakeMediaVerifier();
        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, null, "photo-001")) with
        {
            Title = " "
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ValidateCreatorClueMediaAsync(
                CreatorId, draft));

        Assert.Empty(verifier.Calls);
    }

    // TEST 08 - Trim media reference

    [Fact]
    public async Task MediaId_ShouldBeTrimmed()
    {
        var verifier = new FakeMediaVerifier();
        verifier.Allow("photo-001");

        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, null, "  photo-001  "));

        await service.ValidateCreatorClueMediaAsync(
            CreatorId, draft);

        var call = Assert.Single(verifier.Calls);

        Assert.Equal("photo-001", call.MediaId);
    }

    // TEST 09 - Cancellation

    [Fact]
    public async Task CancelledRequest_ShouldStopValidation()
    {
        var verifier = new FakeMediaVerifier();
        var service = new GameClueMediaOwnershipService(
            verifier);

        var draft = NewDraft(
            new GameDraftStageDto(
                1, null, "photo-001"));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            () => service.ValidateCreatorClueMediaAsync(
                CreatorId,
                draft,
                cancellation.Token));

        Assert.Empty(verifier.Calls);
    }

    // TEST 10 - Missing verifier

    [Fact]
    public void MissingVerifier_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GameClueMediaOwnershipService(
                null!));
    }
}
