
using System;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Application.Modules.TripSafety.Interfaces;

namespace Wayhunter.Api.Services.TripSafety;

public sealed class TripSafetyTripAccessService
    : ITripSessionRecoveryAccessChecker
{
    private readonly ITripMembershipRevocationRepository _repository;

    public TripSafetyTripAccessService(
        ITripMembershipRevocationRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task EnsureOwnerAsync(
        Guid tripPlanId,
        string authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user is required.");
        }

        var trip = await _repository.GetTripPlanByIdAsync(
            tripPlanId,
            cancellationToken);

        if (trip is null)
        {
            throw new System.Collections.Generic.KeyNotFoundException(
                "Trip plan was not found.");
        }

        if (!string.Equals(
                trip.OwnerUserId,
                authenticatedUserId.Trim(),
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the trip owner can perform this operation.");
        }
    }

    public async Task<bool> CanRecoverAsync(
        string authenticatedUserId,
        Guid tripPlanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty ||
            string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            return false;
        }

        var userId = authenticatedUserId.Trim();

        var trip = await _repository.GetTripPlanByIdAsync(
            tripPlanId,
            cancellationToken);

        if (trip is null)
        {
            return false;
        }

        if (string.Equals(
                trip.OwnerUserId,
                userId,
                StringComparison.Ordinal))
        {
            return true;
        }

        var member = await _repository.GetMemberAsync(
            tripPlanId,
            userId,
            cancellationToken);

        return member is not null &&
               member.IsActive &&
               member.TripPlanId == tripPlanId &&
               string.Equals(
                   member.MemberUserId,
                   userId,
                   StringComparison.Ordinal);
    }
}
