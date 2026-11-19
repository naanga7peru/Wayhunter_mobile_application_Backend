
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Wayhunter.Infrastructure.Integrations.Ai;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans/ai")]
public sealed class AiTripPlansController : ControllerBase
{
    private readonly GenerateAiTripPlanService _service;
    private readonly InMemoryAiTripDailyQuota _quota;
    private readonly IOptions<AiTripProviderOptions> _options;

    public AiTripPlansController(
        GenerateAiTripPlanService service,
        InMemoryAiTripDailyQuota quota,
        IOptions<AiTripProviderOptions> options)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));

        _quota = quota ??
            throw new ArgumentNullException(nameof(quota));

        _options = options ??
            throw new ArgumentNullException(nameof(options));
    }

    // POST /api/mobile/trip-plans/ai/generate
    [HttpPost("generate")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Generate(
        [FromBody] AiTripItineraryRequest request,
        CancellationToken cancellationToken)
    {
        // Identity must come from authenticated claims.
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User, out var userId))
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return BadRequest(new
            {
                code = "INVALID_AI_TRIP_REQUEST"
            });
        }

        // Invalid requests must not consume quota.
        try
        {
            AiTripItineraryValidator.ValidateRequest(request);
        }
        catch (ArgumentException)
        {
            return BadRequest(new
            {
                code = "INVALID_AI_TRIP_REQUEST"
            });
        }

        var settings = _options.Value;

        // Disabled by default. Fail closed.
        if (!settings.Enabled ||
            settings.Validate().Count != 0)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { code = "AI_TRIP_UNAVAILABLE" });
        }

        // Reserve quota before calling the external provider.
        if (!_quota.TryAcquire(
                userId,
                settings.MaxRequestsPerUserPerDay))
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { code = "AI_DAILY_LIMIT_REACHED" });
        }

        try
        {
            var result = await _service.GenerateAsync(
                request, cancellationToken);

            // Proposal only. No trip is saved or started.
            return Ok(result);
        }
        catch (TimeoutException)
        {
            return StatusCode(
                StatusCodes.Status504GatewayTimeout,
                new { code = "AI_PROVIDER_TIMEOUT" });
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { code = "AI_PROVIDER_FAILED" });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { code = "AI_PROVIDER_FAILED" });
        }
        catch (ArgumentException)
        {
            // Invalid AI output must not reach Flutter.
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { code = "INVALID_AI_RESPONSE" });
        }
    }
}
