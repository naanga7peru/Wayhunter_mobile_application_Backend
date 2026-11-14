
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Validators;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class GameService
{
    private readonly IGameRepository _gameRepository;

    public GameService(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository
            ?? throw new ArgumentNullException(
                nameof(gameRepository));
    }

    // =====================================
    // CREATE GAME DRAFT
    // =====================================

    public async Task<Game> CreateGameDraftAsync(
        string creatorUserId,
        CreateGameDraftDto request,
        CancellationToken cancellationToken = default)
    {
        var game = BuildValidatedDraft(
            Guid.NewGuid(),
            creatorUserId,
            request);

        await _gameRepository.AddAsync(
            game,
            cancellationToken);

        return game;
    }

    // =====================================
    // READ GAME DRAFT
    // =====================================

    public async Task<Game> GetGameDraftAsync(
        Guid gameId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));

        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException(
                "User ID is required.",
                nameof(actorUserId));

        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
            throw new KeyNotFoundException(
                "Game not found.");

        if (!string.Equals(
            game.CreatorUserId,
            actorUserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the creator can access this draft.");
        }

        if (game.Status != GameStatus.Draft)
            throw new InvalidOperationException(
                "Game is not in Draft status.");

        return game;
    }

    // =====================================
    // UPDATE GAME DRAFT
    // =====================================

    public async Task<Game> UpdateGameDraftAsync(
        Guid gameId,
        string actorUserId,
        CreateGameDraftDto request,
        CancellationToken cancellationToken = default)
    {
        // First verify that this user owns the draft.
        var existingGame = await GetGameDraftAsync(
            gameId,
            actorUserId,
            cancellationToken);

        // Validate the complete replacement BEFORE
        // changing the existing stored game.
        var validatedDraft = BuildValidatedDraft(
            existingGame.Id,
            actorUserId,
            request);

        // Update game details.
        existingGame.UpdateDraft(
            actorUserId,
            validatedDraft.Title!,
            validatedDraft.Description,
            validatedDraft.DurationMinutes!.Value);

        // Update stages and clues.
        existingGame.ReplaceDraftStages(
            actorUserId,
            validatedDraft.DraftSteps,
            validatedDraft.DraftHints);

        await _gameRepository.UpdateAsync(
            existingGame,
            cancellationToken);

        return existingGame;
    }

    // =====================================
    // BUILD AND VALIDATE DRAFT
    // =====================================

    private static Game BuildValidatedDraft(
        Guid gameId,
        string creatorUserId,
        CreateGameDraftDto request)
    {
        GameDraftValidator.Validate(request);

        var game = Game.CreateDraft(
            gameId,
            creatorUserId,
            request.Title,
            request.Description,
            request.DurationMinutes);

        var steps = new List<GameStep>();
        var hints = new List<GameHint>();

        foreach (var stageDto in request.Stages)
        {
            var step = new GameStep(
                Guid.NewGuid(),
                game.Id,
                stageDto.StepNumber);

            var hint = new GameHint(
                Guid.NewGuid(),
                step.Id,
                stageDto.TextHint,
                stageDto.PhotoMediaId);

            steps.Add(step);
            hints.Add(hint);
        }

        game.ReplaceDraftStages(
            creatorUserId,
            steps,
            hints);

        return game;
    }

    // =====================================
    // EXISTING PENDING GAME CREATION
    // =====================================

    public async Task<Game> CreatePendingGameAsync(
        string creatorUserId,
        CancellationToken cancellationToken = default)
    {
        var game = new Game(
            Guid.NewGuid(),
            creatorUserId);

        await _gameRepository.AddAsync(
            game,
            cancellationToken);

        return game;
    }

    // =====================================
    // EXISTING ADMIN APPROVAL
    // =====================================

    public async Task ApproveGameCreationAsync(
        Guid gameId,
        DateTimeOffset approvedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));

        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
            throw new KeyNotFoundException(
                "Game not found.");

        game.ApproveCreation(approvedAtUtc);

        await _gameRepository.UpdateAsync(
            game,
            cancellationToken);
    }
}
