
using System;

namespace Wayhunter.Domain.Modules.Games.Entities;

public sealed class GameHint
{
    public Guid Id { get; }

    public Guid GameStepId { get; }

    public string? TextHint { get; }

    public string? PhotoMediaId { get; }

    public GameHint(
        Guid id,
        Guid gameStepId,
        string? textHint,
        string? photoMediaId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Hint ID is required.");

        if (gameStepId == Guid.Empty)
            throw new ArgumentException(
                "Game Step ID is required.");

        if (string.IsNullOrWhiteSpace(textHint) &&
            string.IsNullOrWhiteSpace(photoMediaId))
        {
            throw new ArgumentException(
                "Text hint or photo hint is required.");
        }

        Id = id;
        GameStepId = gameStepId;

        TextHint = string.IsNullOrWhiteSpace(textHint)
            ? null
            : textHint.Trim();

        PhotoMediaId = string.IsNullOrWhiteSpace(photoMediaId)
            ? null
            : photoMediaId.Trim();
    }
}
