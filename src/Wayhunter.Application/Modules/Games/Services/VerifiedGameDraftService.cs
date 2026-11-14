
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class VerifiedGameDraftService
{
    private readonly GameService _gameService;
    private readonly GameClueMediaOwnershipService _mediaService;

    public VerifiedGameDraftService(
        GameService gameService,
        GameClueMediaOwnershipService mediaService)
    {
        _gameService = gameService
            ?? throw new ArgumentNullException(
                nameof(gameService));

        _mediaService = mediaService
            ?? throw new ArgumentNullException(
                nameof(mediaService));
    }

    // =====================================
    // CREATE VERIFIED GAME DRAFT
    // =====================================

    public async Task<Game> CreateDraftAsync(
        string creatorUserId,
        CreateGameDraftDto request,
        CancellationToken cancellationToken = default)
    {
        // Check all clue media before saving the draft.
        await _mediaService.ValidateCreatorClueMediaAsync(
            creatorUserId,
            request,
            cancellationToken);

        // Save only after validation succeeds.
        return await _gameService.CreateGameDraftAsync(
            creatorUserId,
            request,
            cancellationToken);
    }

    // =====================================
    // UPDATE VERIFIED GAME DRAFT
    // =====================================

    public async Task<Game> UpdateDraftAsync(
        Guid gameId,
        string actorUserId,
        CreateGameDraftDto request,
        CancellationToken cancellationToken = default)
    {
        // Check draft ownership and status first.
        await _gameService.GetGameDraftAsync(
            gameId,
            actorUserId,
            cancellationToken);

        // Verify replacement clue media.
        await _mediaService.ValidateCreatorClueMediaAsync(
            actorUserId,
            request,
            cancellationToken);

        // Update only after checks succeed.
        return await _gameService.UpdateGameDraftAsync(
            gameId,
            actorUserId,
            request,
            cancellationToken);
    }
}
