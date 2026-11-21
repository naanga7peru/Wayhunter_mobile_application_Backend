using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

public sealed class GetSuggestedTripPlansService
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    private readonly ISuggestedTripPlansReader _reader;

    public GetSuggestedTripPlansService(ISuggestedTripPlansReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<SuggestedTripPlanDto?> GetByIdAsync(
        string authenticatedUserId, Guid tripPlanId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedUserId);
        if (tripPlanId == Guid.Empty)
            throw new ArgumentException("Valid trip plan ID is required.", nameof(tripPlanId));

        var plan = await _reader.GetEligibleByIdAsync(authenticatedUserId, tripPlanId, cancellationToken);
        return plan is not null && plan.TripPlanId == tripPlanId && IsValid(plan) ? plan : null;
    }

    private static bool IsValid(SuggestedTripPlanDto plan) =>
        plan.TripPlanId != Guid.Empty &&
        plan.CompletedAtUtc != default &&
        plan.EndDate >= plan.StartDate;

    public async Task<IReadOnlyList<SuggestedTripPlanDto>> GetAsync(
        string authenticatedUserId,
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new ArgumentException(
                "Authenticated User ID is required.",
                nameof(authenticatedUserId));
        }

        if (limit < 1 || limit > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {MaxLimit}.");
        }

        // The reader MUST filter in SQL: active membership + completed
        // M3 session + non-cancelled trip. No client-driven visibility.
        var results = await _reader.GetEligibleAsync(
            authenticatedUserId,
            limit,
            cancellationToken);

        // Defense in depth for malformed source data. An empty result is
        // safer than generating suggestions from unfinished sessions.
        return results
            .Where(IsValid)
            .Take(limit)
            .ToArray();
    }
}
