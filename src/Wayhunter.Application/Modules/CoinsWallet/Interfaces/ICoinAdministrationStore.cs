using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

public interface ICoinAdministrationStore
{
    Task<CoinLedgerCorrectionResponse> ReverseLedgerEntryAsync(
        string actorUserId, Guid ledgerEntryId,
        ReverseCoinLedgerEntryRequest request, DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<CoinRuleVersionResponse> CreateRuleVersionAsync(
        string actorUserId, CreateCoinRuleVersionRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CoinRuleVersionResponse>> GetRuleVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<CoinOperationsReport> GetOperationsReportAsync(
        CancellationToken cancellationToken = default);

    Task<CoinReconciliationReport> GetReconciliationReportAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
