using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TripSafety.Notifications;

namespace Wayhunter.Api.Controllers.Admin.TripSafety;

public sealed record NotificationAdminSendRequest(NotificationAdminCommand Command, bool ConfirmBulk, Guid? PreviewReceiptId);

[ApiController]
[Authorize(Policy = NotificationServiceCollectionExtensions.AdminPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController(NotificationComposerService composer) : NotificationControllerBase
{
    [HttpPost("preview")]
    public Task<IActionResult> Preview(NotificationAdminCommand command, CancellationToken ct) =>
        RunAsync(async actor => Ok(await composer.PreviewAsync(actor, command, ct)));

    [HttpPost("schedules")]
    public Task<IActionResult> Schedule(NotificationAdminSendRequest request, CancellationToken ct) =>
        RunAsync(async actor => Ok(await composer.ScheduleAsync(actor, request.Command, request.ConfirmBulk,
            request.PreviewReceiptId, ct)));

    [HttpPost("schedules/{jobId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid jobId, NotificationOperationRequest request, CancellationToken ct) =>
        RunAsync(async actor => await composer.CancelAsync(actor, jobId, request.Revision, request.OperationId, ct)
            ? NoContent() : NotFound());

    [HttpGet("schedules/{jobId:guid}/summary")]
    public Task<IActionResult> Summary(Guid jobId, CancellationToken ct) => RunAsync(async actor =>
    {
        var summary = await composer.SummaryAsync(actor, jobId, ct);
        return summary is null ? NotFound() : Ok(summary);
    });
}
