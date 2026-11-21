
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Wayhunter.Api.Contracts;
using Wayhunter.Api.Hubs.TripSafety;
using Wayhunter.Api.Services.TripSafety;

using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

public sealed record TripInvitationDecisionApiResponse(
    Guid InvitationId,
    TripInvitationStatus Status,
    Guid? MemberId);

public sealed record TripMemberRemovalApiResponse(
    bool Removed);

[ApiController]
[Authorize]
public sealed class TripSafetyMobileActionsController
    : ControllerBase
{
    private readonly LiveTripSessionService _sessions;
    private readonly LiveTripSessionRecoveryService _recovery;
    private readonly TripInvitationDecisionService _invitations;
    private readonly TripMembershipRevocationService _members;
    private readonly ILiveTripSessionRepository _sessionRepository;
    private readonly TripSafetyTripAccessService _access;
    private readonly TripLocationSubscriptionCleanup? _cleanup;

    public TripSafetyMobileActionsController(
        LiveTripSessionService sessions,
        LiveTripSessionRecoveryService recovery,
        TripInvitationDecisionService invitations,
        TripMembershipRevocationService members,
        ILiveTripSessionRepository sessionRepository,
        TripSafetyTripAccessService access,
        TripLocationSubscriptionCleanup? cleanup = null)
    {
        _sessions = sessions ??
            throw new ArgumentNullException(nameof(sessions));

        _recovery = recovery ??
            throw new ArgumentNullException(nameof(recovery));

        _invitations = invitations ??
            throw new ArgumentNullException(nameof(invitations));

        _members = members ??
            throw new ArgumentNullException(nameof(members));

        _sessionRepository = sessionRepository ??
            throw new ArgumentNullException(
                nameof(sessionRepository));

        _access = access ??
            throw new ArgumentNullException(nameof(access));

        _cleanup = cleanup;
    }

    private bool TryUserId(out string userId)
    {
        return TrustedIdentityAccessContract.TryResolveUserId(
            User,
            out userId);
    }

    private static bool IsExpectedError(
        Exception exception)
    {
        return exception is ArgumentException
            or KeyNotFoundException
            or UnauthorizedAccessException
            or InvalidOperationException;
    }

    private static IActionResult ErrorResponse(
        Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException =>
                new StatusCodeResult(
                    StatusCodes.Status403Forbidden),

            KeyNotFoundException =>
                new NotFoundResult(),

            ArgumentException error =>
                new BadRequestObjectResult(new
                {
                    message = error.Message
                }),

            InvalidOperationException error =>
                new ConflictObjectResult(new
                {
                    message = error.Message
                }),

            _ => throw exception
        };
    }

    // ==============================================
    // LIVE TRIP SESSION
    // ==============================================

    // POST /api/mobile/trips/{tripPlanId}/sessions
    // Only the trip owner may start a trip.

    [HttpPost("api/mobile/trips/{tripPlanId:guid}/sessions")]
    public async Task<IActionResult> Start(
        Guid tripPlanId,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _access.EnsureOwnerAsync(
                tripPlanId,
                userId,
                cancellationToken);

            var session = await _sessions.StartAsync(
                tripPlanId,
                cancellationToken);

            return Ok(TripSessionDto.FromDomain(session));
        }
        catch (Exception exception)
            when (IsExpectedError(exception))
        {
            return ErrorResponse(exception);
        }
    }

    private async Task<IActionResult> ChangeSessionAsync(
        Guid tripPlanId,
        Guid sessionId,
        Func<Guid, CancellationToken, Task<LiveTripSession>> action,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _access.EnsureOwnerAsync(
                tripPlanId,
                userId,
                cancellationToken);

            // Session must belong to the requested trip.

            var existing =
                await _sessionRepository.GetByIdAsync(
                    sessionId,
                    cancellationToken);

            if (existing is null ||
                existing.TripPlanId != tripPlanId)
            {
                return NotFound();
            }

            var updated = await action(
                sessionId,
                cancellationToken);

            return Ok(TripSessionDto.FromDomain(updated));
        }
        catch (Exception exception)
            when (IsExpectedError(exception))
        {
            return ErrorResponse(exception);
        }
    }

    // ==============================================
    // PAUSE TRIP
    // ==============================================

    [HttpPost(
        "api/mobile/trips/{tripPlanId:guid}/sessions/{sessionId:guid}/pause")]
    public Task<IActionResult> Pause(
        Guid tripPlanId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ChangeSessionAsync(
            tripPlanId,
            sessionId,
            _sessions.PauseAsync,
            cancellationToken);
    }

    // ==============================================
    // RESUME TRIP
    // ==============================================

    [HttpPost(
        "api/mobile/trips/{tripPlanId:guid}/sessions/{sessionId:guid}/resume")]
    public Task<IActionResult> Resume(
        Guid tripPlanId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ChangeSessionAsync(
            tripPlanId,
            sessionId,
            _sessions.ResumeAsync,
            cancellationToken);
    }

    // ==============================================
    // FINISH TRIP
    // ==============================================

    [HttpPost(
        "api/mobile/trips/{tripPlanId:guid}/sessions/{sessionId:guid}/finish")]
    public async Task<IActionResult> Finish(
        Guid tripPlanId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        // Existing owner + trip/session checks
        // are preserved in ChangeSessionAsync.

        var response = await ChangeSessionAsync(
            tripPlanId,
            sessionId,
            _sessions.FinishAsync,
            cancellationToken);

        // Only perform cleanup after successful
        // session finish. Failed/unauthorized
        // requests cannot remove subscriptions.

        if (response is OkObjectResult &&
            _cleanup is not null)
        {
            await _cleanup.RevokeSessionAsync(
                tripPlanId,
                sessionId,
                CancellationToken.None);
        }

        return response;
    }

    // ==============================================
    // RECOVER LIVE TRIP SESSION
    // ==============================================

    [HttpGet(
        "api/mobile/trips/{tripPlanId:guid}/sessions/{sessionId:guid}/recover")]
    public async Task<IActionResult> Recover(
        Guid tripPlanId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var authorized = await _access.CanRecoverAsync(
                userId,
                tripPlanId,
                cancellationToken);

            if (!authorized)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden);
            }

            var session =
                await _sessionRepository.GetByIdAsync(
                    sessionId,
                    cancellationToken);

            if (session is null ||
                session.TripPlanId != tripPlanId)
            {
                return NotFound();
            }

            var result = await _recovery.RecoverAsync(
                sessionId,
                userId,
                cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (Exception exception)
            when (IsExpectedError(exception))
        {
            return ErrorResponse(exception);
        }
    }

    // ==============================================
    // INVITATION DECISIONS
    // ==============================================

    private async Task<IActionResult> ChangeInvitationAsync(
        Guid invitationId,
        string action,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            if (action == "accept")
            {
                var member = await _invitations.AcceptAsync(
                    invitationId,
                    userId,
                    cancellationToken);

                return Ok(new TripInvitationDecisionApiResponse(
                    invitationId,
                    TripInvitationStatus.Accepted,
                    member.Id));
            }

            TripInvitation invitation;

            if (action == "reject")
            {
                invitation = await _invitations.RejectAsync(
                    invitationId,
                    userId,
                    cancellationToken);
            }
            else if (action == "cancel")
            {
                invitation = await _invitations.CancelAsync(
                    invitationId,
                    userId,
                    cancellationToken);
            }
            else
            {
                throw new ArgumentException(
                    "Unsupported invitation action.");
            }

            return Ok(new TripInvitationDecisionApiResponse(
                invitation.Id,
                invitation.Status,
                null));
        }
        catch (Exception exception)
            when (IsExpectedError(exception))
        {
            return ErrorResponse(exception);
        }
    }

    // ==============================================
    // ACCEPT INVITATION
    // ==============================================

    [HttpPost(
        "api/mobile/trip-invitations/{invitationId:guid}/accept")]
    public Task<IActionResult> AcceptInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        return ChangeInvitationAsync(
            invitationId,
            "accept",
            cancellationToken);
    }

    // ==============================================
    // REJECT INVITATION
    // ==============================================

    [HttpPost(
        "api/mobile/trip-invitations/{invitationId:guid}/reject")]
    public Task<IActionResult> RejectInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        return ChangeInvitationAsync(
            invitationId,
            "reject",
            cancellationToken);
    }

    // ==============================================
    // CANCEL INVITATION
    // ==============================================

    [HttpPost(
        "api/mobile/trip-invitations/{invitationId:guid}/cancel")]
    public Task<IActionResult> CancelInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        return ChangeInvitationAsync(
            invitationId,
            "cancel",
            cancellationToken);
    }

    // ==============================================
    // REMOVE TRIP MEMBER
    // ==============================================

    [HttpDelete(
        "api/mobile/trips/{tripPlanId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid tripPlanId,
        string memberUserId,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!Guid.TryParseExact(
                memberUserId,
                "D",
                out var targetId) ||
            targetId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Valid member User ID is required."
            });
        }

        try
        {
            var removed = await _members.RemoveAsync(
                tripPlanId,
                userId,
                targetId.ToString("D"),
                cancellationToken);

            // A successful membership revocation
            // removes this user's local live GPS
            // subscriptions for the trip.

            if (removed && _cleanup is not null)
            {
                await _cleanup.RevokeTripMemberAsync(
                    tripPlanId,
                    targetId.ToString("D"),
                    CancellationToken.None);
            }

            return Ok(
                new TripMemberRemovalApiResponse(removed));
        }
        catch (Exception exception)
            when (IsExpectedError(exception))
        {
            return ErrorResponse(exception);
        }
    }
}
