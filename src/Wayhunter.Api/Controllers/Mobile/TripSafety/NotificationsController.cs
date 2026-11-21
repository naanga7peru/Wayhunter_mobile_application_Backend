using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TripSafety.Notifications;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

public sealed record NotificationDeviceRequest(Guid OperationId, string? Token, bool PushAllowed)
{
    public override string ToString() => $"Notification device operation {OperationId:D}";
}
public sealed record NotificationOperationRequest(Guid OperationId, int Revision = 1);

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/mobile/notifications")]
public sealed class NotificationsController(INotificationInbox inbox, INotificationDeviceRegistry devices,
    NotificationReminderService reminders, TimeProvider clock) : NotificationControllerBase
{
    [HttpGet]
    public Task<IActionResult> Inbox([FromQuery] int limit, CancellationToken ct) => RunAsync(async user =>
    {
        if (limit is < 0 or > 100)
            return BadRequest();
        return Ok(await inbox.ReadAsync(user, limit == 0 ? 30 : limit, ct));
    });

    [HttpPut("{eventId:guid}/read")]
    public Task<IActionResult> Read(Guid eventId, CancellationToken ct) => RunAsync(async user =>
        eventId == Guid.Empty ? BadRequest() :
        await inbox.MarkReadAsync(user, eventId, clock.GetUtcNow(), ct) ? NoContent() : NotFound());

    [HttpGet("preferences")]
    public Task<IActionResult> Preferences(CancellationToken ct) =>
        RunAsync(async user => Ok(await inbox.GetPreferencesAsync(user, ct)));

    [HttpPut("preferences")]
    public Task<IActionResult> SetPreference(NotificationPreference preference, CancellationToken ct) => RunAsync(async user =>
    {
        if (!Enum.IsDefined(preference.Category))
            return BadRequest();
        await inbox.SetPreferenceAsync(user, preference, ct);
        return NoContent();
    });

    [HttpPut("devices/{deviceId:guid}")]
    public Task<IActionResult> Register(Guid deviceId, NotificationDeviceRequest request, CancellationToken ct) => RunAsync(async user =>
    {
        NotificationInputs.ValidateDevice(deviceId, request.OperationId, request.Token);
        return Ok(await devices.RegisterOrRotateAsync(user, deviceId, request.OperationId, request.Token!,
            request.PushAllowed, ct));
    });

    [HttpPost("devices/{deviceId:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid deviceId, NotificationOperationRequest request, CancellationToken ct) => RunAsync(async user =>
    {
        NotificationInputs.CheckOperation(deviceId, request.OperationId);
        return await devices.RevokeAsync(user, deviceId, request.OperationId, ct) ? NoContent() : NotFound();
    });

    [HttpPut("reminders")]
    public Task<IActionResult> Schedule(NotificationReminderCommand command, CancellationToken ct) =>
        RunAsync(async user => Ok(await reminders.ScheduleAsync(user, command, ct)));

    [HttpPost("reminders/{jobId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid jobId, NotificationOperationRequest request, CancellationToken ct) =>
        RunAsync(async user => await reminders.CancelAsync(user, jobId, request.Revision, request.OperationId, ct)
            ? NoContent() : NotFound());
}

public abstract class NotificationControllerBase : ControllerBase
{
    protected async Task<IActionResult> RunAsync(Func<string, Task<IActionResult>> action)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();
        try { return await action(userId); }
        catch (NotificationUnavailableException) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (NotificationConflictException) { return Conflict(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException) { return BadRequest(); }
    }
}
