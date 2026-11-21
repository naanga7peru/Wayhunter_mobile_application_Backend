using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

/// <summary>
/// Atomically replaces only an Advertisement image URL.
/// The expected URL protects against concurrent replacements.
/// </summary>
public interface IAdvertisementImageUpdateRepository
{
    Task<AdvertisementDto?> UpdateImageUrlAsync(
        Guid advertisementId,
        string expectedImageUrl,
        string trustedHttpsUrl,
        CancellationToken cancellationToken = default);
}
