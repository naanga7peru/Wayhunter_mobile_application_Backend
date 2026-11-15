
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Entities;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

// M6 Phase 2 SQL persistence models.
// These are not domain services or database migrations.

public sealed class SampleVoucherOfferRow
{
    public Guid Id { get; set; }
    public string OfferCode { get; set; } = string.Empty;
    public long CostCoins { get; set; }
    public decimal ReferenceValueLkr { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset ValidFromUtc { get; set; }
    public DateTimeOffset ValidUntilUtc { get; set; }
}

public sealed class SampleVoucherRedemptionRequestRow
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public Guid OfferId { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public long CostCoins { get; set; }

    public SampleVoucherRequestStatusDraft Status
    { get; set; }

    public Guid? AdminDecisionId { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
}

public sealed class SampleVoucherInventoryRow
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public string SampleCode { get; set; } = string.Empty;
    public Guid? AssignedRequestId { get; set; }
}

public sealed class SampleVoucherIssuanceRow
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid InventoryId { get; set; }

    public SampleVoucherIssueStatusDraft Status
    { get; set; }

    public string? QrPayload { get; set; }
    public string? ImageReference { get; set; }
    public DateTimeOffset? IssuedAtUtc { get; set; }
}

public enum CoinRewardOutboxState
{
    Pending = 1,
    Processing = 2,
    Delivered = 3
}

public sealed class CoinRewardOutboxRow
{
    public Guid EventId { get; set; }

    public CoinRewardType RewardType { get; set; }
    public string SourceRecordId { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;

    public int CoinsAmount { get; set; }

    public DateTimeOffset EligibilityConfirmedAtUtc
    { get; set; }

    public Guid CorrelationId { get; set; }

    public string RuleVersion { get; set; } = string.Empty;
    public string? ApprovalRecordId { get; set; }
    public string? GameAttemptId { get; set; }

    public CoinRewardOutboxState Status { get; set; }
        = CoinRewardOutboxState.Pending;

    public int AttemptNumber { get; set; }

    public DateTimeOffset NextDueAtUtc { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public string? ClaimToken { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public enum CoinRewardConsumerDeliveryState
{
    Pending = 1,
    Delivered = 2,
    RetryPending = 3
}

public sealed class CoinRewardConsumerDeliveryRow
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }

    public string ConsumerName { get; set; } = string.Empty;

    public CoinRewardConsumerDeliveryState Status
    { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
}
