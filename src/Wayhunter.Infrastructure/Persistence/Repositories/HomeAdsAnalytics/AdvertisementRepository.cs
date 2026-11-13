
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;

public sealed class AdvertisementRepository : IAdvertisementRepository
{
    private readonly HomeAdsAnalyticsDbContext _dbContext;

    public AdvertisementRepository(
        HomeAdsAnalyticsDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // Get all advertisements
    public async Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var advertisements = await _dbContext.Advertisements
            .AsNoTracking()
            .Select(ad => new AdvertisementDto
            {
                Id = ad.Id,
                Title = ad.Title,
                ImageUrl = ad.ImageUrl,
                DestinationUrl = ad.DestinationUrl,
                StartAtUtc = ad.StartAtUtc,
                EndAtUtc = ad.EndAtUtc,
                IsEnabled = ad.IsEnabled
            })
            .ToListAsync(cancellationToken);

        return advertisements;
    }

    // Get one advertisement by ID
    public async Task<AdvertisementDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Advertisements
            .AsNoTracking()
            .Where(ad => ad.Id == id)
            .Select(ad => new AdvertisementDto
            {
                Id = ad.Id,
                Title = ad.Title,
                ImageUrl = ad.ImageUrl,
                DestinationUrl = ad.DestinationUrl,
                StartAtUtc = ad.StartAtUtc,
                EndAtUtc = ad.EndAtUtc,
                IsEnabled = ad.IsEnabled
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
