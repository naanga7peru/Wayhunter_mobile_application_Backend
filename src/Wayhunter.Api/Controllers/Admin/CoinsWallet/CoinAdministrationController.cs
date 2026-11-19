using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Authorization;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Api.Controllers.Admin.CoinsWallet;

[ApiController]
[Authorize(Policy = CoinsWalletAdministratorPolicy.Name)]
[Route("api/admin/coins-wallet")]
public sealed class CoinAdministrationController : ControllerBase
{
    private readonly SampleVoucherAdminService _service;

    public CoinAdministrationController(SampleVoucherAdminService service) =>
        _service = service ?? throw new ArgumentNullException(nameof(service));

    [HttpPost("ledger/{ledgerEntryId:guid}/reverse")]
    public Task<ActionResult<CoinLedgerCorrectionResponse>> ReverseLedger(
        Guid ledgerEntryId,
        [FromBody] ReverseCoinLedgerEntryRequest request,
        CancellationToken cancellationToken) => Execute(() =>
        _service.ReverseLedgerEntryAsync(
            User, ledgerEntryId, request, cancellationToken));

    [HttpPost("rule-versions")]
    public Task<ActionResult<CoinRuleVersionResponse>> CreateRuleVersion(
        [FromBody] CreateCoinRuleVersionRequest request,
        CancellationToken cancellationToken) => Execute(() =>
        _service.CreateRuleVersionAsync(User, request, cancellationToken));

    [HttpGet("rule-versions")]
    public Task<ActionResult<IReadOnlyList<CoinRuleVersionResponse>>>
        GetRuleVersions(CancellationToken cancellationToken) => Execute(() =>
            _service.GetRuleVersionsAsync(User, cancellationToken));

    [HttpGet("reports/operations")]
    public Task<ActionResult<CoinOperationsReport>> GetOperationsReport(
        CancellationToken cancellationToken) => Execute(() =>
            _service.GetOperationsReportAsync(User, cancellationToken));

    [HttpGet("reports/reconciliation")]
    public Task<ActionResult<CoinReconciliationReport>> GetReconciliationReport(
        CancellationToken cancellationToken) => Execute(() =>
            _service.GetReconciliationReportAsync(User, cancellationToken));

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}
