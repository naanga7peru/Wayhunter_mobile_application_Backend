using Wayhunter.Domain.Modules.CoinsWallet.Entities;

namespace Wayhunter.Application.Modules.CoinsWallet.Dtos;

public sealed record CreateSampleVoucherOfferRequest(
    Guid OfferId,
    string OfferCode,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset ValidUntilUtc);

public sealed record AddSampleVoucherInventoryRequest(
    Guid InventoryId,
    Guid OfferId,
    string VoucherCode,
    string SampleImageReference,
    DateTimeOffset ExpiresAtUtc);

public sealed record SampleVoucherInventoryResponse(
    Guid InventoryId,
    Guid OfferId,
    DateTimeOffset ExpiresAtUtc,
    bool IsAssigned,
    Guid? AssignedRequestId,
    string SampleImageReference);

public sealed record SampleVoucherAdminQueueItem(
    Guid RequestId,
    Guid OfferId,
    string OwnerUserId,
    string OwnerName,
    long CostCoins,
    DateTimeOffset RequestedAtUtc,
    int AvailableStock);

public sealed record SampleVoucherAdminQueueResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<SampleVoucherAdminQueueItem> Items);

public sealed record SampleVoucherDecisionRequest(
    Guid DecisionId,
    string Reason);

public sealed record SampleVoucherIssuanceResponse(
    Guid RequestId,
    SampleVoucherIssueStatusDraft Status,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    int AttemptCount,
    string? LastErrorCode);

public sealed record MyIssuedSampleVoucherResponse(
    Guid RequestId,
    SampleVoucherIssueStatusDraft Status,
    decimal ReferenceValueLkr,
    string? VoucherCode,
    string? QrPayload,
    string? SampleImageReference,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? UsedAtUtc);
