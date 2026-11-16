
using System;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// Audit and resulting progress snapshot
/// for a Game proof review decision.
/// </summary>
public sealed record GameStageProofDecisionResultDto(
    Guid DecisionId,
    Guid GameId,
    Guid AttemptId,
    Guid EvidenceId,
    int StepNumber,
    bool Approved,
    string ReviewerUserId,
    string? Reason,
    DateTimeOffset ReviewedAtUtc,
    int CurrentStepNumber,
    GameProgressStatus ProgressStatus,
    int RejectedEvidenceCount);
