
using System;
using System.Linq;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Wayhunter.Infrastructure.Persistence.Seeding;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementDemoSeedTests
{
    [Fact]
    public void DemoSeeds_ShouldHaveTwoDistinctFixedIds()
    {
        var records = AdvertisementDemoSeed.CreateDemoRecords();

        Assert.Equal(2, records.Count);
        Assert.Equal(2, records.Select(x => x.Id).Distinct().Count());

        Assert.Contains(
            records,
            x => x.Id == AdvertisementDemoSeed.HomeBannerId);

        Assert.Contains(
            records,
            x => x.Id == AdvertisementDemoSeed.SponsoredPoiId);

        var secondCall = AdvertisementDemoSeed.CreateDemoRecords();

        Assert.Equal(
            records.Select(x => x.Id),
            secondCall.Select(x => x.Id));
    }

    [Fact]
    public void DemoSeeds_ShouldHaveCorrectPlacements()
    {
        var records = AdvertisementDemoSeed.CreateDemoRecords();

        Assert.Contains(
            records,
            x => x.Placement == AdvertisementPlacement.HomeBanner);

        Assert.Contains(
            records,
            x => x.Placement == AdvertisementPlacement.SponsoredPoi);
    }

    [Fact]
    public void DemoSeeds_ShouldStartAsDraftAndDisabled()
    {
        var records = AdvertisementDemoSeed.CreateDemoRecords();

        Assert.All(records, record =>
        {
            Assert.Equal(AdvertisementStatus.Draft, record.Status);
            Assert.False(record.IsEnabled);
            Assert.Equal(0L, record.ImpressionCount);
            Assert.Equal(0L, record.ClickCount);
        });
    }

    [Fact]
    public void DemoSeeds_ShouldHaveValidHttpsUrlsAndDateWindows()
    {
        var records = AdvertisementDemoSeed.CreateDemoRecords();

        Assert.All(records, record =>
        {
            Assert.True(
                Uri.TryCreate(
                    record.ImageUrl,
                    UriKind.Absolute,
                    out var imageUri));

            Assert.Equal(Uri.UriSchemeHttps, imageUri!.Scheme);

            Assert.True(record.EndAtUtc > record.StartAtUtc);
            Assert.Equal(TimeSpan.Zero, record.StartAtUtc.Offset);
            Assert.Equal(TimeSpan.Zero, record.EndAtUtc.Offset);
        });
    }

    [Fact]
    public void MissingRecords_ShouldSkipExistingDemoIds()
    {
        var missing = AdvertisementDemoSeed.GetMissingRecords(
            new[] { AdvertisementDemoSeed.HomeBannerId });

        var record = Assert.Single(missing);

        Assert.Equal(
            AdvertisementDemoSeed.SponsoredPoiId,
            record.Id);
    }

    [Fact]
    public void MissingRecords_ShouldReturnEmptyWhenBothExist()
    {
        var missing = AdvertisementDemoSeed.GetMissingRecords(
            new[]
            {
                AdvertisementDemoSeed.HomeBannerId,
                AdvertisementDemoSeed.SponsoredPoiId
            });

        Assert.Empty(missing);
    }

    [Fact]
    public void MissingRecords_ShouldNotChangeUnrelatedAdminRecords()
    {
        var adminAdvertisementId = Guid.NewGuid();

        var missing = AdvertisementDemoSeed.GetMissingRecords(
            new[] { adminAdvertisementId });

        Assert.Equal(2, missing.Count);

        Assert.DoesNotContain(
            missing,
            x => x.Id == adminAdvertisementId);
    }

    [Fact]
    public void MissingRecords_ShouldRejectNullExistingIds()
    {
        Assert.Throws<ArgumentNullException>(
            () => AdvertisementDemoSeed.GetMissingRecords(null!));
    }
}
