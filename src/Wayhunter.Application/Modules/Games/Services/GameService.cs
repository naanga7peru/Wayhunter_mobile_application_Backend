
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;

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
