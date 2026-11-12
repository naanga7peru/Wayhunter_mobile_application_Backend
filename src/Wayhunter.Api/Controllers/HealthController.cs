using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts.Responses;

namespace Wayhunter.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse(
        "Wayhunter.Api", "ok", clock.GetUtcNow()));
}
