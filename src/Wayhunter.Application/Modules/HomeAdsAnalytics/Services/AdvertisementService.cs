
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
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

    // Get all active advertisements
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

    // Get active advertisements for one placement
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
}
