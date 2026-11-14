
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Validators;

public static class GameDraftValidator
{
    public static void Validate(CreateGameDraftDto? draft)
    {
        if (draft is null)
            throw new ArgumentNullException(nameof(draft));

        if (string.IsNullOrWhiteSpace(draft.Title))
            throw new ArgumentException(
                "Game title is required.");

        if (draft.DurationMinutes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(draft.DurationMinutes),
                "Game duration must be greater than zero.");

        if (draft.Stages is null || draft.Stages.Count == 0)
            throw new ArgumentException(
                "At least one game stage is required.");

        var stepNumbers = new HashSet<int>();

        foreach (var stage in draft.Stages)
        {
            if (stage is null)
                throw new ArgumentException(
                    "Game stage cannot be null.");

            if (stage.StepNumber <= 0)
                throw new ArgumentException(
                    "Stage number must be positive.");

            if (!stepNumbers.Add(stage.StepNumber))
                throw new ArgumentException(
                    "Duplicate stage numbers are not allowed.");

            if (string.IsNullOrWhiteSpace(stage.TextHint) &&
                string.IsNullOrWhiteSpace(stage.PhotoMediaId))
            {
                throw new ArgumentException(
                    "Each stage requires a text or photo hint.");
            }
        }
    }
}
