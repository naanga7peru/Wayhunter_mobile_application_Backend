
using System;

namespace Wayhunter.Domain.Modules.Games.Entities;

public sealed class GameStep
{
    public Guid Id { get; }
    public Guid GameId { get; }
    public int StepNumber { get; }

    public GameStep(
        Guid id,
        Guid gameId,
        int stepNumber)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Step ID is required.");

        if (gameId == Guid.Empty)
            throw new ArgumentException(
                "Game ID is required.");

        if (stepNumber <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(stepNumber),
                "Step number must be greater than zero.");

        Id = id;
        GameId = gameId;
        StepNumber = stepNumber;
    }
}
