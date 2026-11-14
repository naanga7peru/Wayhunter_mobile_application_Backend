
namespace Wayhunter.Application.Modules.CoinsWallet.Dtos;

public sealed record CoinRewardCorrectionRequest(
    Guid OriginalRewardSourceId,
    string RequestedByAdminUserId,
    string Reason,
    Guid CorrelationId
);
