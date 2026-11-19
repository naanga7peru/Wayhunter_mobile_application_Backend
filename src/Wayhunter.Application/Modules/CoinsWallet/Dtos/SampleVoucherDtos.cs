using Wayhunter.Domain.Modules.CoinsWallet.Entities;

namespace Wayhunter.Application.Modules.CoinsWallet.Dtos;

public sealed record SampleVoucherOfferResponse(
    Guid OfferId,
    string OfferCode,
    long CostCoins,
    decimal ReferenceValueLkr,
    DateTimeOffset ValidUntilUtc,
    int AvailableStock,
    bool IsEligible);

public sealed record CreateSampleVoucherRequest(
    Guid RequestId,
    Guid OfferId);

public sealed record CancelSampleVoucherRequest(string Reason);

public sealed record SampleVoucherRequestResponse(
    Guid RequestId,
    Guid OfferId,
    string OwnerUserId,
    string OwnerName,
    long CostCoins,
    SampleVoucherRequestStatusDraft Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionReason);
