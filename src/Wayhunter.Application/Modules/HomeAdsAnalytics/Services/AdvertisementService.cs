
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

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
}
