
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

public interface IAdvertisementRepository
{
    Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<AdvertisementDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
