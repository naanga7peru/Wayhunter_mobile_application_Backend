using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Wayhunter.Api.Controllers.Mobile.TripSafety;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Enums;
namespace Wayhunter.Api.Services.TripSafety;
public sealed class TripFinishReminderFilter : IAsyncActionFilter
{
    private readonly ITripFinishedReminderGuard _reminders;
    private readonly ILogger<TripFinishReminderFilter> _logger;
    public TripFinishReminderFilter(ITripFinishedReminderGuard reminders, ILogger<TripFinishReminderFilter> logger)
    {
        _reminders = reminders ?? throw new ArgumentNullException(nameof(reminders));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var executed = await next();
        if (executed.Canceled || executed.Exception is not null) return;
        if (context.Controller is not TripSafetyMobileActionsController) return;
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor || descriptor.ActionName != nameof(TripSafetyMobileActionsController.Finish)) return;
        if (executed.Result is not OkObjectResult ok || ok.Value is not TripSessionDto session || session.Status != LiveTripStatus.Finished) return;
        try
        {
            await _reminders.MarkFinishedAsync(session.TripPlanId, session.SessionId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "M3 post-finish reminder guard update failed.");
        }
    }
}
