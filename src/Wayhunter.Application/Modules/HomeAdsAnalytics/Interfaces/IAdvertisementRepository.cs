
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

public interface IAdvertisementRepository
{
    Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
        DateTimeOffset nowUtc,
        AdvertisementPlacement? placement = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<AdvertisementDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
