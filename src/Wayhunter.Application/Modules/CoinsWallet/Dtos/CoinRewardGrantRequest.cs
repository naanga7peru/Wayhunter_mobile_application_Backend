
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Dtos;

public sealed record CoinRewardGrantRequest(
    Guid EventId,
    CoinRewardType RewardType,
    string SourceRecordId,
    string RecipientUserId,
    int CoinsAmount,
    DateTimeOffset EligibilityConfirmedAtUtc,
    Guid CorrelationId,
    string RuleVersion,
    string? ApprovalRecordId = null,
    string? GameAttemptId = null
);
