using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Authorization;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Api.Controllers.Admin.CoinsWallet;

[ApiController]
[Authorize(Policy = CoinsWalletAdministratorPolicy.Name)]
[Route("api/admin/coins-wallet/vouchers")]
public sealed class SampleVoucherAdminController : ControllerBase
{
    private readonly SampleVoucherAdminService _service;

    public SampleVoucherAdminController(
        SampleVoucherAdminService service)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));
    }

    [HttpGet("requests")]
    public async Task<ActionResult<SampleVoucherAdminQueueResponse>>
        GetQueue(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        => await Execute(() => _service.GetQueueAsync(
            User, page, pageSize, cancellationToken));

    [HttpPost("offers")]
    public async Task<IActionResult> CreateOffer(
        [FromBody] CreateSampleVoucherOfferRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.CreateOfferAsync(
                User, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("inventory")]
    public async Task<ActionResult<SampleVoucherInventoryResponse>>
        AddInventory(
            [FromBody] AddSampleVoucherInventoryRequest request,
            CancellationToken cancellationToken)
        => await Execute(() => _service.AddInventoryAsync(
            User, request, cancellationToken));

    [HttpPost("requests/{requestId:guid}/approve")]
    public async Task<ActionResult<SampleVoucherRequestResponse>>
        Approve(
            Guid requestId,
            [FromBody] SampleVoucherDecisionRequest decision,
            CancellationToken cancellationToken)
        => await Execute(() => _service.ApproveAsync(
            User, requestId, decision, cancellationToken));

    [HttpPost("requests/{requestId:guid}/reject")]
    public async Task<ActionResult<SampleVoucherRequestResponse>>
        Reject(
            Guid requestId,
            [FromBody] SampleVoucherDecisionRequest decision,
            CancellationToken cancellationToken)
        => await Execute(() => _service.RejectAsync(
            User, requestId, decision, cancellationToken));

    [HttpPost("requests/{requestId:guid}/issue")]
    public async Task<ActionResult<SampleVoucherIssuanceResponse>>
        Issue(
            Guid requestId,
            CancellationToken cancellationToken)
        => await Execute(() => _service.IssueAsync(
            User, requestId, cancellationToken));

    [HttpPost("requests/{requestId:guid}/use")]
    public async Task<ActionResult<SampleVoucherIssuanceResponse>> Consume(
        Guid requestId,
        [FromBody] ConsumeSampleVoucherRequest request,
        CancellationToken cancellationToken) =>
        await Execute(() => _service.ConsumeAsync(
            User, requestId, request, cancellationToken));

    [HttpPost("expiry/run")]
    public async Task<ActionResult<ExpireSampleVouchersResponse>> ExpireDue(
        [FromQuery] int maxCount = 100,
        CancellationToken cancellationToken = default) =>
        await Execute(() => _service.ExpireDueAsync(
            User, maxCount, cancellationToken));

    private async Task<ActionResult<T>> Execute<T>(
        Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
