
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Interfaces;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed class TripSharingConsentService
{
    private readonly ITripSharingConsentRepository _repository;
    private readonly TimeProvider _timeProvider;

    public TripSharingConsentService(
        ITripSharingConsentRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    // User ID must come from trusted authentication claims.
    // Never accept the target User ID from the request body.
    public Task<GpsConsentDto> SetMyConsentAsync(
        Guid tripPlanId,
        Guid sessionId,
        string? authenticatedUserId,
        bool grant,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid trip plan ID is required.",
                nameof(tripPlanId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid session ID is required.",
                nameof(sessionId));
        }

        return _repository.SetMyConsentAsync(
            tripPlanId,
            sessionId,
            authenticatedUserId.Trim(),
            grant,
            _timeProvider.GetUtcNow().ToUniversalTime(),
            cancellationToken);
    }
}
