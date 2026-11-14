
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed class LiveTripSessionRecoveryService
{
    private readonly ILiveTripSessionRepository _repository;

    private readonly ITripSessionRecoveryAccessChecker
        _accessChecker;

    public LiveTripSessionRecoveryService(
        ILiveTripSessionRepository repository,
        ITripSessionRecoveryAccessChecker accessChecker)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _accessChecker = accessChecker
            ?? throw new ArgumentNullException(nameof(accessChecker));
    }

    public async Task<TripSessionDto?> RecoverAsync(
        Guid sessionId,
        string? authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Session ID is required.",
                nameof(sessionId));
        }

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        var session = await _repository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            return null;
        }

        var authorized = await _accessChecker.CanRecoverAsync(
            authenticatedUserId.Trim(),
            session.TripPlanId,
            cancellationToken);

        if (!authorized)
        {
            throw new UnauthorizedAccessException(
                "User cannot recover this trip session.");
        }

        return session.Status switch
        {
            LiveTripStatus.Active =>
                TripSessionDto.FromDomain(session),

            LiveTripStatus.Paused =>
                TripSessionDto.FromDomain(session),

            LiveTripStatus.Finished => null,

            _ => throw new InvalidOperationException(
                "Unknown trip session status.")
        };
    }
}
