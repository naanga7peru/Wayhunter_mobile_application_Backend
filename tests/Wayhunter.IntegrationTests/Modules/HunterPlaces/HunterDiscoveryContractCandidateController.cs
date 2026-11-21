using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.HunterPlaces.Queries;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

// Test-assembly routes. Visibility and pagination conventions are provisional.
[ApiController]
[Authorize]
[Route("api/mobile/v1/hunter")]
public sealed class HunterDiscoveryContractCandidateController : ControllerBase
{
    [HttpGet("places")]
    public IActionResult Search(
        [FromServices] HunterDiscoveryCandidate discovery, string? search = null,
        string? district = null, int offset = 0, int limit = 20)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out _))
            return Unauthorized();

        return offset < 0 || limit is < 1 or > 100
            ? BadRequest() : Ok(discovery.Search(search, district, offset, limit));
    }

    [HttpGet("places/{id:guid}")]
    public IActionResult Find(Guid id, [FromServices] HunterDiscoveryCandidate discovery)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out _))
            return Unauthorized();

        var place = discovery.Find(id);
        return place is null ? NotFound() : Ok(place);
    }

    [HttpGet("leaderboard")]
    public IActionResult Leaderboard(
        [FromServices] IReadOnlyList<HunterLedgerCandidate> ledger, int offset = 0, int limit = 20)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out _))
            return Unauthorized();

        if (offset < 0 || limit is < 1 or > 100)
            return BadRequest();

        try
        {
            return Ok(HunterLeaderboardCandidate.Rank(ledger, offset, limit).Select(row => new { row.Rank, HunterCoins = row.Coins }));
        }
        catch (InvalidOperationException)
        {
            return StatusCode(503);
        }
    }
}
