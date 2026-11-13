
using System;

namespace Wayhunter.Domain.Modules.Games.Entities;

public sealed class GameAttempt
{
    public Guid Id { get; }

    public Guid GameId { get; }

    public string PlayerUserId { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public GameAttempt(
        Guid id,
        Guid gameId,
        string playerUserId,
        DateTimeOffset startedAtUtc)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Attempt ID is required.");

        if (gameId == Guid.Empty)
            throw new ArgumentException(
                "Game ID is required.");

        if (string.IsNullOrWhiteSpace(playerUserId))
            throw new ArgumentException(
                "Player ID is required.");

        if (startedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "UTC timestamp is required.");

        Id = id;
        GameId = gameId;
        PlayerUserId = playerUserId.Trim();
        StartedAtUtc = startedAtUtc;
    }
}
