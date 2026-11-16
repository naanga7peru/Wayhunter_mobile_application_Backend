
using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Infrastructure.Persistence.Seeding;

public static class AdvertisementDemoSeed
{
    public static readonly Guid HomeBannerId =
        Guid.Parse("a1000000-0000-4000-8000-000000000001");

    public static readonly Guid SponsoredPoiId =
        Guid.Parse("a1000000-0000-4000-8000-000000000002");

    private static readonly DateTimeOffset StartAtUtc =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset EndAtUtc =
        new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<Advertisement> CreateDemoRecords()
    {
        var homeBanner = new Advertisement(
            HomeBannerId,
            "Demo Home Banner",
            "https://example.com/wayhunter/demo-home-banner.jpg",
            "https://example.com/wayhunter",
            StartAtUtc,
            EndAtUtc);

        homeBanner.SetPlacement(
            AdvertisementPlacement.HomeBanner);

        var sponsoredPoi = new Advertisement(
            SponsoredPoiId,
            "Demo Sponsored POI",
            "https://example.com/wayhunter/demo-sponsored-poi.jpg",
            "https://example.com/wayhunter/places",
            StartAtUtc,
            EndAtUtc);

        sponsoredPoi.SetPlacement(
            AdvertisementPlacement.SponsoredPoi);

        return new[]
        {
            homeBanner,
            sponsoredPoi
        };
    }

    // Prepare only missing demo records.
    // Existing administrator-managed records are never modified.
    // No database operation is performed here.
    public static IReadOnlyList<Advertisement> GetMissingRecords(
        IEnumerable<Guid> existingAdvertisementIds)
    {
        ArgumentNullException.ThrowIfNull(
            existingAdvertisementIds);

        var existingIds = existingAdvertisementIds.ToHashSet();

        return CreateDemoRecords()
            .Where(ad => !existingIds.Contains(ad.Id))
            .ToArray();
    }
}
