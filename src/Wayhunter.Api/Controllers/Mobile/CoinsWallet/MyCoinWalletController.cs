
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Queries;

namespace Wayhunter.Api.Controllers.Mobile.CoinsWallet;

[ApiController]
[Authorize]
[Route("api/mobile/coins-wallet")]
public sealed class MyCoinWalletController : ControllerBase
{
    private readonly GetMyCoinWalletService _service;

    public MyCoinWalletController(
        GetMyCoinWalletService service)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// GET /api/mobile/coins-wallet/me
    /// Returns only the authenticated user's Coins wallet.
    ///
    /// Authentication and repository dependency injection
    /// must be integrated before this endpoint can go live.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<MyCoinWalletResponse>> GetMine(
        CancellationToken cancellationToken)
    {
        try
        {
            var wallet = await _service.GetMineAsync(
                User,
                cancellationToken);

            if (wallet is null)
            {
                return NotFound();
            }

            return Ok(wallet);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
