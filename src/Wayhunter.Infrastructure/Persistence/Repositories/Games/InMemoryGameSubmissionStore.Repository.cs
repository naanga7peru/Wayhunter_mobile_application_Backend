
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed partial class InMemoryGameSubmissionStore
    : IGameRepository, IGameDraftDeletionRepository
{
    public Task<Game?> GetByIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _games.TryGetValue(gameId, out var game);

            return Task.FromResult(game);
        }
    }

    public Task AddAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Dictionary.Add rejects duplicate Game IDs.
            _games.Add(game.Id, game);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_games.TryGetValue(game.Id, out var existingGame))
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            // Prevent replacing an existing Game with
            // another creator's Game using the same ID.
            if (!string.Equals(
                existingGame.CreatorUserId,
                game.CreatorUserId,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Game creator identity cannot be changed.");
            }

            _games[game.Id] = game;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_games.TryGetValue(gameId, out var game))
            {
                throw new KeyNotFoundException(
                    "Game not found.");
            }

            // Repository-level defensive protection.
            if (game.Status != GameStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft games can be deleted.");
            }

            _games.Remove(gameId);
        }

        return Task.CompletedTask;
    }
}
