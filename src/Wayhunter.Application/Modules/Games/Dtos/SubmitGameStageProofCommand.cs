
using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// M5-MS05-T02
/// Request contract for submitting proof
/// for the player's current Game stage.
/// </summary>
public sealed record SubmitGameStageProofCommand(
    Guid GameId,
    Guid AttemptId,
    int StepNumber,
    string MediaId,
    string CaptureSessionId
);
