using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TripSafety.Memories;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

public sealed record CreateTripMemoryRequest(Guid OperationId, Guid? TripStopId, string? Caption,
    TripMemoryVisibility Visibility = TripMemoryVisibility.Private);
public sealed record BeginTripMemoryUploadRequest(Guid OperationId, string? ContentType, long ContentLength);

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/mobile/trips/{tripPlanId:guid}/memories")]
public sealed class TripMemoriesController(TripMemoryService memories, TripMemoryMediaService media) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Create(Guid tripPlanId, CreateTripMemoryRequest request, CancellationToken ct) =>
        ForActiveUserAsync(async userId =>
        {
            var result = await memories.CreateAsync(tripPlanId, userId, request.OperationId, request.TripStopId,
                request.Caption, request.Visibility, ct);
            if (!result.Created)
                return Ok(result.Memory);

            return CreatedAtAction(nameof(Get), new { tripPlanId, memoryId = result.Memory.MemoryId }, result.Memory);
        }, ct);

    [HttpGet("{memoryId:guid}")]
    public Task<IActionResult> Get(Guid tripPlanId, Guid memoryId, CancellationToken ct) =>
        ForActiveUserAsync(async userId => Ok(await memories.GetAsync(tripPlanId, memoryId, userId, ct)), ct);

    [HttpGet]
    public Task<IActionResult> List(Guid tripPlanId, CancellationToken ct) =>
        ForActiveUserAsync(async userId => Ok(await memories.ListAsync(tripPlanId, userId, ct)), ct);

    [HttpDelete("{memoryId:guid}")]
    public Task<IActionResult> Delete(Guid tripPlanId, Guid memoryId, CancellationToken ct) =>
        ForActiveUserAsync(async userId =>
        {
            await memories.DeleteAsync(tripPlanId, memoryId, userId, ct);
            return NoContent();
        }, ct);

    [HttpPost("{memoryId:guid}/media/uploads")]
    public Task<IActionResult> BeginUpload(Guid tripPlanId, Guid memoryId, BeginTripMemoryUploadRequest request,
        CancellationToken ct) =>
        ForActiveUserAsync(async userId => Ok(await media.BeginAsync(tripPlanId, memoryId, userId,
            request.OperationId, request.ContentType, request.ContentLength, ct)), ct);

    [HttpPost("{memoryId:guid}/media/uploads/{operationId:guid}/complete")]
    public Task<IActionResult> CompleteUpload(Guid tripPlanId, Guid memoryId, Guid operationId, CancellationToken ct) =>
        ForActiveUserAsync(async userId => Ok(await media.CompleteAsync(tripPlanId, memoryId, userId, operationId, ct)), ct);

    private async Task<IActionResult> ForActiveUserAsync(Func<string, Task<IActionResult>> run, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        try
        {
            return await run(userId);
        }
        catch (TripMemoryUnavailableException) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (TripMemoryConflictException) { return Conflict(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return BadRequest(); }
    }
}
