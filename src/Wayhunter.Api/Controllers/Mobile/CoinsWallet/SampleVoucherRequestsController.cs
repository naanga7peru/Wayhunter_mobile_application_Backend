using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Api.Controllers.Mobile.CoinsWallet;

[ApiController]
[Authorize]
[Route("api/mobile/coins-wallet/vouchers")]
public sealed class SampleVoucherRequestsController : ControllerBase
{
    private readonly SampleVoucherRequestService _service;

    public SampleVoucherRequestsController(
        SampleVoucherRequestService service)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));
    }

    [HttpGet("offers")]
    public async Task<ActionResult<
        IReadOnlyList<SampleVoucherOfferResponse>>> GetOffers(
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetOffersAsync(
                User,
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("offers/{offerId:guid}")]
    public async Task<ActionResult<SampleVoucherOfferResponse>> GetOffer(
        Guid offerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetOfferAsync(
                User,
                offerId,
                cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("requests")]
    public async Task<ActionResult<SampleVoucherRequestResponse>>
        RequestVoucher(
            [FromBody] CreateSampleVoucherRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.RequestAsync(
                User,
                request,
                cancellationToken);

            return Ok(result);
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

    [HttpGet("requests/{requestId:guid}")]
    public async Task<ActionResult<SampleVoucherRequestResponse>>
        GetRequest(
            Guid requestId,
            CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetAsync(
                User,
                requestId,
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("requests/{requestId:guid}/cancel")]
    public async Task<ActionResult<SampleVoucherRequestResponse>>
        CancelRequest(
            Guid requestId,
            [FromBody] CancelSampleVoucherRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.CancelAsync(
                User,
                requestId,
                request.Reason,
                cancellationToken));
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

    [HttpGet("requests/{requestId:guid}/issued-voucher")]
    public async Task<ActionResult<MyIssuedSampleVoucherResponse>>
        GetIssuedVoucher(
            Guid requestId,
            CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetVoucherAsync(
                User,
                requestId,
                cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
