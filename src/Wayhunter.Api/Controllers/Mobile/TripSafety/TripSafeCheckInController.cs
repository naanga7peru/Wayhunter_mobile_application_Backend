using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
namespace Wayhunter.Api.Controllers.Mobile.TripSafety;
public sealed record TripSafeCheckInApiResponse(Guid CheckInId, Guid TripPlanId, Guid SessionId, string UserId, DateTimeOffset CheckedInAtUtc, bool Created);
[ApiController]
[Authorize]
public sealed class TripSafeCheckInController : ControllerBase
{
    private readonly TripSafeCheckInService _service;
    private readonly TripSafeSignalRNotifier _notifier;
    public TripSafeCheckInController(TripSafeCheckInService service, TripSafeSignalRNotifier notifier)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }
    [HttpPost("api/mobile/trips/{tripPlanId:guid}/sessions/{sessionId:guid}/im-safe")]
    public async Task<IActionResult> MarkSafe(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId)) return Unauthorized();
        try
        {
            var result = await _service.MarkSafeAsync(tripPlanId, sessionId, userId, cancellationToken);
            if (result.Created) await _notifier.NotifyOwnerAsync(result.CheckIn, cancellationToken);
            var item = result.CheckIn;
            return Ok(new TripSafeCheckInApiResponse(item.CheckInId, item.TripPlanId, item.SessionId, item.UserId, item.CheckedInAtUtc, result.Created));
        }
        catch (TripSafetyCheckInUnavailableException) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Durable safe check-in persistence is not configured." }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }
}
public sealed class TripSafeSignalRNotifier
{
    private readonly IHubContext<TripLocationHub> _hub;
    private readonly TripLocationSubscriberRegistry _registry;
    private readonly ITripMembershipRevocationRepository _members;
    private readonly ITripLiveLocationAccessReader _gpsAccess;
    private readonly ILogger<TripSafeSignalRNotifier> _logger;
    public TripSafeSignalRNotifier(IHubContext<TripLocationHub> hub, TripLocationSubscriberRegistry registry, ITripMembershipRevocationRepository members, ITripLiveLocationAccessReader gpsAccess, ILogger<TripSafeSignalRNotifier> logger)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _members = members ?? throw new ArgumentNullException(nameof(members));
        _gpsAccess = gpsAccess ?? throw new ArgumentNullException(nameof(gpsAccess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task NotifyOwnerAsync(TripSafeCheckIn checkIn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkIn);
        try
        {
            var trip = await _members.GetTripPlanByIdAsync(checkIn.TripPlanId, cancellationToken);
            if (trip is null) return;
            var subscribers = _registry.Snapshot(checkIn.TripPlanId, checkIn.SessionId);
            foreach (var subscriber in subscribers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(subscriber.UserId, trip.OwnerUserId, StringComparison.Ordinal)) continue;
                try
                {
                    await _gpsAccess.EnsureAllowedAsync(checkIn.TripPlanId, checkIn.SessionId, subscriber.UserId, cancellationToken);
                }
                catch (UnauthorizedAccessException) { _registry.RemoveIfUnchanged(subscriber); continue; }
                catch (InvalidOperationException) { _registry.RemoveIfUnchanged(subscriber); continue; }
                catch (KeyNotFoundException) { _registry.RemoveIfUnchanged(subscriber); continue; }
                if (_registry.Find(subscriber.ConnectionId) != subscriber) continue;
                await _hub.Clients.Client(subscriber.ConnectionId).SendAsync("TripMemberMarkedSafe", checkIn, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { _logger.LogWarning("M3 safe check-in notification cancelled after recording."); }
        catch (Exception exception) { _logger.LogWarning(exception, "M3 safe check-in notification failed after recording."); }
    }
}
