
namespace Wayhunter.Domain.Modules.CoinsWallet.Entities;

// Draft model. Not yet registered in EF Core.
public sealed record CoinWalletSchemaDraft
{
    public Guid Id { get; }
    public string UserId { get; }
    public long PostedCoins { get; }
    public long ReservedCoins { get; }
    public long LifetimeEarnedCoins { get; }

    public CoinWalletSchemaDraft(
        Guid id,
        string userId,
        long postedCoins,
        long reservedCoins,
        long lifetimeEarnedCoins)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Wallet ID required.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID required.");

        if (postedCoins < 0 ||
            reservedCoins < 0 ||
            lifetimeEarnedCoins < 0 ||
            reservedCoins > postedCoins)
            throw new ArgumentOutOfRangeException(
                nameof(reservedCoins),
                "Invalid wallet balance.");

        Id = id;
        UserId = userId.Trim();
        PostedCoins = postedCoins;
        ReservedCoins = reservedCoins;
        LifetimeEarnedCoins = lifetimeEarnedCoins;
    }
}

public sealed record CoinLedgerEntrySchemaDraft
{
    public Guid Id { get; }
    public Guid WalletId { get; }
    public long DeltaCoins { get; }
    public string SourceRecordId { get; }
    public string RuleVersion { get; }
    public DateTimeOffset RecordedAtUtc { get; }

    public CoinLedgerEntrySchemaDraft(
        Guid id,
        Guid walletId,
        long deltaCoins,
        string sourceRecordId,
        string ruleVersion,
        DateTimeOffset recordedAtUtc)
    {
        if (id == Guid.Empty || walletId == Guid.Empty)
            throw new ArgumentException(
                "Ledger and Wallet IDs required.");

        if (deltaCoins == 0)
            throw new ArgumentException(
                "Ledger delta cannot be zero.");

        if (string.IsNullOrWhiteSpace(sourceRecordId))
            throw new ArgumentException(
                "Immutable source record ID required.");

        if (string.IsNullOrWhiteSpace(ruleVersion))
            throw new ArgumentException(
                "Rule version required.");

        if (recordedAtUtc == default ||
            recordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "UTC timestamp required.");

        Id = id;
        WalletId = walletId;
        DeltaCoins = deltaCoins;
        SourceRecordId = sourceRecordId.Trim();
        RuleVersion = ruleVersion.Trim();
        RecordedAtUtc = recordedAtUtc;
    }
}

public sealed record CoinReservationSchemaDraft
{
    public Guid Id { get; }
    public Guid WalletId { get; }
    public Guid RequestId { get; }
    public long ReservedCoins { get; }

    public CoinReservationSchemaDraft(
        Guid id,
        Guid walletId,
        Guid requestId,
        long reservedCoins)
    {
        if (id == Guid.Empty ||
            walletId == Guid.Empty ||
            requestId == Guid.Empty)
            throw new ArgumentException(
                "Reservation IDs required.");

        if (reservedCoins <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(reservedCoins));

        Id = id;
        WalletId = walletId;
        RequestId = requestId;
        ReservedCoins = reservedCoins;
    }
}

public sealed record CoinRuleVersionSchemaDraft
{
    public Guid Id { get; }
    public string VersionCode { get; }
    public long VoucherCostCoins { get; }
    public decimal VoucherReferenceLkr { get; }

    public CoinRuleVersionSchemaDraft(
        Guid id,
        string versionCode,
        long voucherCostCoins,
        decimal voucherReferenceLkr)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Rule version ID required.");

        if (string.IsNullOrWhiteSpace(versionCode))
            throw new ArgumentException(
                "Version code required.");

        if (voucherCostCoins <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(voucherCostCoins));

        if (voucherReferenceLkr < 0)
            throw new ArgumentOutOfRangeException(
                nameof(voucherReferenceLkr));

        Id = id;
        VersionCode = versionCode.Trim();
        VoucherCostCoins = voucherCostCoins;
        VoucherReferenceLkr = voucherReferenceLkr;
    }
}
