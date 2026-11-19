using System.Security.Claims;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;

namespace Wayhunter.Application.Modules.CoinsWallet.Commands;

public sealed class SampleVoucherAdminService
{
    private readonly ISampleVoucherAdminStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly string _ruleVersion;
    private readonly ICoinAdministrationStore? _administrationStore;

    public SampleVoucherAdminService(
        ISampleVoucherAdminStore store,
        TimeProvider timeProvider,
        string ruleVersion,
        ICoinAdministrationStore? administrationStore = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ??
            throw new ArgumentNullException(nameof(timeProvider));
        _ruleVersion = string.IsNullOrWhiteSpace(ruleVersion)
            ? throw new ArgumentException("Rule version is required.")
            : ruleVersion.Trim();
        _administrationStore = administrationStore;
    }

    public Task<SampleVoucherAdminQueueResponse> GetQueueAsync(
        ClaimsPrincipal? administrator,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActor(administrator);
        return _store.GetQueueAsync(
            page,
            pageSize,
            cancellationToken);
    }

    public Task CreateOfferAsync(
        ClaimsPrincipal? administrator,
        CreateSampleVoucherOfferRequest request,
        CancellationToken cancellationToken = default)
    {
        return _store.CreateOfferAsync(
            RequireActor(administrator),
            request,
            cancellationToken);
    }

    public Task<SampleVoucherInventoryResponse> AddInventoryAsync(
        ClaimsPrincipal? administrator,
        AddSampleVoucherInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        return _store.AddInventoryAsync(
            RequireActor(administrator),
            request,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<SampleVoucherRequestResponse> ApproveAsync(
        ClaimsPrincipal? administrator,
        Guid requestId,
        SampleVoucherDecisionRequest decision,
        CancellationToken cancellationToken = default)
    {
        return _store.ApproveAsync(
            RequireActor(administrator),
            requestId,
            decision,
            _timeProvider.GetUtcNow(),
            _ruleVersion,
            cancellationToken);
    }

    public Task<SampleVoucherRequestResponse> RejectAsync(
        ClaimsPrincipal? administrator,
        Guid requestId,
        SampleVoucherDecisionRequest decision,
        CancellationToken cancellationToken = default)
    {
        return _store.RejectAsync(
            RequireActor(administrator),
            requestId,
            decision,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<SampleVoucherIssuanceResponse> IssueAsync(
        ClaimsPrincipal? administrator,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return _store.IssueAsync(
            RequireActor(administrator),
            requestId,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<SampleVoucherIssuanceResponse> ConsumeAsync(
        ClaimsPrincipal? administrator,
        Guid requestId,
        ConsumeSampleVoucherRequest request,
        CancellationToken cancellationToken = default) =>
        _store.ConsumeAsync(
            RequireActor(administrator), requestId, request,
            _timeProvider.GetUtcNow(), cancellationToken);

    public async Task<ExpireSampleVouchersResponse> ExpireDueAsync(
        ClaimsPrincipal? administrator,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActor(administrator);
        int count = await _store.ExpireDueAsync(
            _timeProvider.GetUtcNow(), maxCount, cancellationToken);
        return new ExpireSampleVouchersResponse(count);
    }

    public Task<CoinLedgerCorrectionResponse> ReverseLedgerEntryAsync(
        ClaimsPrincipal? administrator,
        Guid ledgerEntryId,
        ReverseCoinLedgerEntryRequest request,
        CancellationToken cancellationToken = default) =>
        RequireAdministrationStore().ReverseLedgerEntryAsync(
            RequireActor(administrator), ledgerEntryId, request,
            _timeProvider.GetUtcNow(), cancellationToken);

    public Task<CoinRuleVersionResponse> CreateRuleVersionAsync(
        ClaimsPrincipal? administrator,
        CreateCoinRuleVersionRequest request,
        CancellationToken cancellationToken = default) =>
        RequireAdministrationStore().CreateRuleVersionAsync(
            RequireActor(administrator), request,
            _timeProvider.GetUtcNow(), cancellationToken);

    public Task<IReadOnlyList<CoinRuleVersionResponse>> GetRuleVersionsAsync(
        ClaimsPrincipal? administrator,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActor(administrator);
        return RequireAdministrationStore().GetRuleVersionsAsync(cancellationToken);
    }

    public Task<CoinOperationsReport> GetOperationsReportAsync(
        ClaimsPrincipal? administrator,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActor(administrator);
        return RequireAdministrationStore().GetOperationsReportAsync(cancellationToken);
    }

    public Task<CoinReconciliationReport> GetReconciliationReportAsync(
        ClaimsPrincipal? administrator,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActor(administrator);
        return RequireAdministrationStore().GetReconciliationReportAsync(
            _timeProvider.GetUtcNow(), cancellationToken);
    }

    private ICoinAdministrationStore RequireAdministrationStore() =>
        _administrationStore ?? throw new InvalidOperationException(
            "Coins administration persistence is unavailable.");

    private static string RequireActor(
        ClaimsPrincipal? administrator)
    {
        if (administrator?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException(
                "Administrator authentication is required.");

        string? actor = administrator
            .FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? administrator.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(actor))
            throw new UnauthorizedAccessException(
                "Trusted administrator identity is required.");

        return actor.Trim();
    }
}
