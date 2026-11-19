namespace Wayhunter.Application.Modules.CoinsWallet.Dtos;

public sealed record ReverseCoinLedgerEntryRequest(
    Guid CorrectionId,
    string Reason,
    string RuleVersion);

public sealed record CoinLedgerCorrectionResponse(
    Guid CorrectionId,
    Guid OriginalEntryId,
    string OwnerUserId,
    long DeltaCoins,
    long PostedCoins,
    long LifetimeEarnedCoins,
    DateTimeOffset RecordedAtUtc);

public sealed record CreateCoinRuleVersionRequest(
    Guid RuleVersionId,
    string VersionCode,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveUntilUtc);

public sealed record CoinRuleVersionResponse(
    Guid RuleVersionId,
    string VersionCode,
    long VoucherCostCoins,
    decimal VoucherReferenceLkr,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveUntilUtc,
    string CreatedByUserId,
    DateTimeOffset CreatedAtUtc);

public sealed record CoinOperationsReport(
    long MintedCoins,
    long DebitedCoins,
    long CurrentReservedCoins,
    long ReleasedReservationCoins,
    int PendingVoucherRequests,
    int ApprovedUnissuedRequests,
    int FailedIssuances,
    int ReconciliationExceptionCount);

public sealed record CoinReconciliationException(
    string Code,
    string ReferenceId,
    string Detail);

public sealed record CoinReconciliationReport(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<CoinReconciliationException> Exceptions);

public sealed record ConsumeSampleVoucherRequest(string Reason);

public sealed record ExpireSampleVouchersResponse(int ExpiredCount);
