
using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// M5-MS06-T05: a trusted reviewer decides
/// an already-submitted Game stage proof.
/// Reviewer identity comes from the
/// authenticated server-side principal.
/// </summary>
public sealed record GameStageProofDecisionCommand(
    Guid DecisionId,
    Guid GameId,
    Guid AttemptId,
    Guid EvidenceId,
    int StepNumber,
    bool Approved,
    string? Reason = null);
