
using System;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Application.Modules.Games.Dtos;

public sealed record GameAdminQueueItemDto(
    Guid GameId,
    string CreatorUserId,
    string CreatorNameSnapshot,
    string GameTitle,
    DateTimeOffset SubmittedAtUtc)
{
    public static GameAdminQueueItemDto FromSubmittedGame(
        Game game,
        UserAccount creator)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(creator);

        // Only submitted games belong in this queue.
        if (game.Status != GameStatus.PendingApproval ||
            !game.SubmittedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Game must be submitted for Admin review.");
        }

        // Creator must be an active account.
        if (creator.Status != UserAccountStatus.Active)
        {
            throw new InvalidOperationException(
                "Creator account must be active.");
        }

        // Match the Game owner with the identity record.
        if (!string.Equals(
            game.CreatorUserId,
            creator.UserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Game creator identity mismatch.");
        }

        if (string.IsNullOrWhiteSpace(game.Title))
        {
            throw new InvalidOperationException(
                "Submitted game must have a title.");
        }

        // Reuse the Identity module snapshot contract.
        var identity = ApprovalIdentitySnapshotDto.Create(
            creator,
            game.Id,
            game.SubmittedAtUtc.Value);

        // Explicit fields for the Admin review queue.
        return new GameAdminQueueItemDto(
            identity.ItemId,
            identity.UserId,
            identity.DisplayNameSnapshot,
            game.Title,
            identity.ServerTimestampUtc);
    }
}
