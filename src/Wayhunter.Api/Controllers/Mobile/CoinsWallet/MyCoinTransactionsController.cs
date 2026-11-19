using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Queries;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Api.Controllers.Mobile.CoinsWallet;

[ApiController]
[Authorize]
[Route("api/mobile/coins-wallet/me/transactions")]
public sealed class MyCoinTransactionsController : ControllerBase
{
    private readonly GetMyCoinTransactionHistoryService _service;

    public MyCoinTransactionsController(
        GetMyCoinTransactionHistoryService service)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));
    }

    [HttpGet]
    public async Task<ActionResult<CoinTransactionHistoryResponse>>
        GetMine(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] CoinTransactionKind? kind = null,
            [FromQuery] CoinRewardType? rewardType = null,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetMineAsync(
                User,
                page,
                pageSize,
                kind,
                rewardType,
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
