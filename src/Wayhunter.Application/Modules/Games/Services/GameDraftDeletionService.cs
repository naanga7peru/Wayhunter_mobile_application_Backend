
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class GameDraftDeletionService
{
    private readonly IGameDraftDeletionRepository _repository;

    public GameDraftDeletionService(
        IGameDraftDeletionRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(
                nameof(repository));
    }

    // Only the creator may delete an unsubmitted draft.
    public async Task DeleteGameDraftAsync(
        Guid gameId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "Game ID is required.",
                nameof(gameId));
        }

        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(actorUserId));
        }

        var game = await _repository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            throw new KeyNotFoundException(
                "Game not found.");
        }

        if (!string.Equals(
            game.CreatorUserId,
            actorUserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the creator can delete this game.");
        }

        if (game.Status != GameStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft games can be deleted.");
        }

        await _repository.DeleteAsync(
            game.Id,
            cancellationToken);
    }
}
