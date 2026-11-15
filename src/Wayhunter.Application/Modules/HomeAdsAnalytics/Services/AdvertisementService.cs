
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Services;

public sealed class AdvertisementService
{
    private readonly IAdvertisementRepository _repository;

    public AdvertisementService(
        IAdvertisementRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    // Existing: Get all active advertisements
    public async Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var advertisements =
            await _repository.GetAllAsync(cancellationToken);

        return advertisements
            .Where(ad =>
                ad.IsEnabled &&
                ad.StartAtUtc <= nowUtc &&
                nowUtc < ad.EndAtUtc)
            .ToArray();
    }

    // Existing: Get active advertisements by placement
    public async Task<IReadOnlyList<AdvertisementDto>>
        GetActiveByPlacementAsync(
            DateTimeOffset nowUtc,
            AdvertisementPlacement placement,
            CancellationToken cancellationToken = default)
    {
        if (placement != AdvertisementPlacement.HomeBanner &&
            placement != AdvertisementPlacement.SponsoredPoi)
        {
            throw new ArgumentOutOfRangeException(
                nameof(placement),
                "Unsupported advertisement placement.");
        }

        var activeAdvertisements =
            await GetActiveAsync(nowUtc, cancellationToken);

        return activeAdvertisements
            .Where(ad => ad.Placement == placement)
            .ToArray();
    }

    // New: Validated internal advertisement query
    public async Task<IReadOnlyList<AdvertisementDto>>
        GetActiveByQueryAsync(
            AdvertisementActiveQueryRequest? request,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
    {
        if (request is null ||
            !AdvertisementActiveQueryRequestValidator.IsValid(request))
        {
            throw new ArgumentException(
                "Invalid advertisement query.",
                nameof(request));
        }

        // District persistence is awaiting the approved schema.
        // Never return unfiltered ads for a district request.
        if (request.District is not null)
        {
            throw new NotSupportedException(
                "Advertisement district targeting is not yet available.");
        }

        // This is an internal as-of query.
        var queryTimeUtc = request.AtUtc ?? nowUtc;

        var advertisements =
            await GetActiveAsync(queryTimeUtc, cancellationToken);

        if (request.Placement.HasValue)
        {
            return advertisements
                .Where(ad =>
                    ad.Placement == request.Placement.Value)
                .ToArray();
        }

        return advertisements;
    }
}
