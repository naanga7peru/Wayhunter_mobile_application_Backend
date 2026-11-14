
using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

public sealed record GameApprovalCommand(
    Guid GameId,
    string AdminUserId,
    string ApprovalRecordId,
    DateTimeOffset ApprovedAtUtc,
    Guid RewardEventId,
    Guid CorrelationId,
    string RewardRuleVersion
);
