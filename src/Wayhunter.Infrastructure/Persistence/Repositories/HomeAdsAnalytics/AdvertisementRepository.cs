
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;

public sealed class AdvertisementRepository
    : IAdvertisementRepository,
      IAdvertisementWriteRepository,
      IAdvertisementImageUpdateRepository
{
    private readonly WayhunterSharedDbContext _dbContext;

    public AdvertisementRepository(
        WayhunterSharedDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // ==========================================
    // READ - All advertisements
    // ==========================================

    public async Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
        DateTimeOffset nowUtc,
        AdvertisementPlacement? placement = null,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Advertisement query time must be UTC.",
                nameof(nowUtc));
        }

        if (placement.HasValue &&
            placement.Value != AdvertisementPlacement.HomeBanner &&
            placement.Value != AdvertisementPlacement.SponsoredPoi)
        {
            throw new ArgumentOutOfRangeException(
                nameof(placement),
                "Unsupported advertisement placement.");
        }

        var query = _dbContext.Advertisements
            .AsNoTracking()
            .Where(ad =>
                ad.IsEnabled &&
                ad.Status == AdvertisementStatus.Active &&
                ad.StartAtUtc <= nowUtc &&
                nowUtc < ad.EndAtUtc);

        if (placement.HasValue)
        {
            query = query.Where(ad => ad.Placement == placement.Value);
        }

        return await query
            .Select(ad => new AdvertisementDto
            {
                Id = ad.Id,
                Title = ad.Title,
                ImageUrl = ad.ImageUrl,
                DestinationUrl = ad.DestinationUrl,
                StartAtUtc = ad.StartAtUtc,
                EndAtUtc = ad.EndAtUtc,
                IsEnabled = ad.IsEnabled,
                Placement = ad.Placement,
                Status = ad.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Advertisements
            .AsNoTracking()
            .Select(ad => new AdvertisementDto
            {
                Id = ad.Id,
                Title = ad.Title,
                ImageUrl = ad.ImageUrl,
                DestinationUrl = ad.DestinationUrl,
                StartAtUtc = ad.StartAtUtc,
                EndAtUtc = ad.EndAtUtc,
                IsEnabled = ad.IsEnabled,
                Placement = ad.Placement,
                Status = ad.Status
            })
            .ToListAsync(cancellationToken);
    }

    // ==========================================
    // READ - One advertisement
    // ==========================================

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
                IsEnabled = ad.IsEnabled,
                Placement = ad.Placement,
                Status = ad.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ==========================================
    // CREATE - Draft advertisement
    // ==========================================

    public async Task<AdvertisementDto> CreateAsync(
        AdvertisementCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdvertisementCreateRequestValidator.IsValid(request))
        {
            throw new ArgumentException(
                "Invalid advertisement create request.",
                nameof(request));
        }

        var advertisement = new Advertisement(
            Guid.NewGuid(),
            request.Title,
            request.ImageUrl,
            request.DestinationUrl,
            request.StartAtUtc,
            request.EndAtUtc);

        advertisement.SetPlacement(request.Placement);

        await _dbContext.Advertisements.AddAsync(
            advertisement,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(advertisement);
    }

    // ==========================================
    // UPDATE - Advertisement details
    // ==========================================

    public async Task<AdvertisementDto?> UpdateAsync(
        Guid id,
        AdvertisementUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdvertisementUpdateRequestValidator.IsValid(request))
        {
            throw new ArgumentException(
                "Invalid advertisement update request.",
                nameof(request));
        }

        var advertisement = await _dbContext.Advertisements
            .FirstOrDefaultAsync(
                ad => ad.Id == id,
                cancellationToken);

        if (advertisement is null)
        {
            return null;
        }

        advertisement.UpdateDetails(
            request.Title,
            request.ImageUrl,
            request.DestinationUrl,
            request.StartAtUtc,
            request.EndAtUtc,
            request.Placement);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(advertisement);
    }

    // ==========================================
    // STATUS - Active / Paused / Archived
    // ==========================================

    public async Task<AdvertisementDto?> ChangeStatusAsync(
        Guid id,
        AdvertisementStatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdvertisementStatusUpdateRequestValidator.IsValid(request))
        {
            throw new ArgumentException(
                "Invalid advertisement status request.",
                nameof(request));
        }

        var advertisement = await _dbContext.Advertisements
            .FirstOrDefaultAsync(
                ad => ad.Id == id,
                cancellationToken);

        if (advertisement is null)
        {
            return null;
        }

        switch (request.Status!.Value)
        {
            case AdvertisementStatus.Active:
                advertisement.Enable();
                break;

            case AdvertisementStatus.Paused:
                if (advertisement.Status is
                    AdvertisementStatus.Draft or
                    AdvertisementStatus.Archived)
                {
                    throw new InvalidOperationException(
                        "Only active or paused advertisements can be paused.");
                }

                advertisement.Disable();
                break;

            case AdvertisementStatus.Archived:
                advertisement.Archive();
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(advertisement);
    }

    // ==========================================
    // IMAGE-ONLY ATOMIC REPLACEMENT
    // ==========================================

    public async Task<AdvertisementDto?> UpdateImageUrlAsync(
        Guid advertisementId,
        string expectedImageUrl,
        string trustedHttpsUrl,
        CancellationToken cancellationToken = default)
    {
        if (advertisementId == Guid.Empty)
            throw new ArgumentException(
                "Invalid advertisement ID.",
                nameof(advertisementId));

        if (string.IsNullOrWhiteSpace(expectedImageUrl))
            throw new ArgumentException(
                "Expected image URL is required.",
                nameof(expectedImageUrl));

        if (!AdvertisementUrlValidator.IsValidImageUrl(
                trustedHttpsUrl))
        {
            throw new ArgumentException(
                "Invalid trusted HTTPS image URL.",
                nameof(trustedHttpsUrl));
        }

        // Read before the conditional update.
        // Never perform a second read after a successful image update.
        var existing = await GetByIdAsync(
            advertisementId,
            cancellationToken);

        if (existing is null)
            return null;

        var updatedRows = await _dbContext.Advertisements
            .Where(ad =>
                ad.Id == advertisementId &&
                ad.ImageUrl == expectedImageUrl &&
                ad.Status != AdvertisementStatus.Archived)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    ad => ad.ImageUrl,
                    trustedHttpsUrl),
                cancellationToken);

        if (updatedRows != 1)
            return null;

        // ExecuteUpdateAsync confirmed one matching row.
        // Return the pre-update snapshot with the committed image URL.
        return existing with
        {
            ImageUrl = trustedHttpsUrl
        };
    }

    // ==========================================
    // ENTITY -> RESPONSE DTO
    // ==========================================

    private static AdvertisementDto ToDto(
        Advertisement advertisement)
    {
        return new AdvertisementDto
        {
            Id = advertisement.Id,
            Title = advertisement.Title,
            ImageUrl = advertisement.ImageUrl,
            DestinationUrl = advertisement.DestinationUrl,
            StartAtUtc = advertisement.StartAtUtc,
            EndAtUtc = advertisement.EndAtUtc,
            IsEnabled = advertisement.IsEnabled,
            Placement = advertisement.Placement,
            Status = advertisement.Status
        };
    }
}
