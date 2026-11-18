
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

public interface IAdvertisementWriteRepository
{
    Task<AdvertisementDto> CreateAsync(
        AdvertisementCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<AdvertisementDto?> UpdateAsync(
        Guid id,
        AdvertisementUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<AdvertisementDto?> ChangeStatusAsync(
        Guid id,
        AdvertisementStatusUpdateRequest request,
        CancellationToken cancellationToken = default);
}
