
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

// SQL persistence models.
// Existing domain draft records remain unchanged.
// These models do not implement wallet transactions.

public sealed class CoinWalletRow
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long PostedCoins { get; set; }
    public long ReservedCoins { get; set; }
    public long LifetimeEarnedCoins { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CoinLedgerEntryRow
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }

    public long DeltaCoins { get; set; }

    // Null for non-reward ledger entries.
    public CoinRewardType? RewardType { get; set; }

    public string SourceRecordId { get; set; } =
        string.Empty;

    public string RuleVersion { get; set; } =
        string.Empty;

    public Guid CorrelationId { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public Guid? CorrectionOfEntryId { get; set; }
}

public enum CoinReservationState
{
    Pending = 1,
    Consumed = 2,
    Released = 3
}

public sealed class CoinReservationRow
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public Guid RequestId { get; set; }

    public long ReservedCoins { get; set; }

    public CoinReservationState Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class CoinRuleVersionRow
{
    public Guid Id { get; set; }

    public string VersionCode { get; set; } =
        string.Empty;

    public long VoucherCostCoins { get; set; }

    public decimal VoucherReferenceLkr { get; set; }
}
