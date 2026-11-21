using System;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class CampaignDomainTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddDays(1);

    [Theory]
    [InlineData(CampaignType.SponsoredGame, null)]
    [InlineData(CampaignType.SponsoredPlace, null)]
    [InlineData(CampaignType.SponsoredMission, null)]
    [InlineData(CampaignType.DistrictPass, "Colombo")]
    public void DraftRetainsConfiguration(CampaignType type, string? district)
    {
        var campaign = Create(type, district);

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(type, campaign.Type);
        Assert.Equal(district, campaign.TargetDistrict);
        Assert.NotEqual(Guid.Empty, campaign.SponsorId);
        Assert.NotEqual(Guid.Empty, campaign.ContentId);
        Assert.Equal(0, campaign.BudgetCoins);
        Assert.Equal(TimeSpan.Zero, campaign.StartAtUtc.Offset);
    }

    [Fact]
    public void InvalidConfigurationIsRejected()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Campaign(Guid.Empty, CampaignType.SponsoredGame, id, id, null, 0, Start, End));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Campaign(id, CampaignType.SponsoredGame, Guid.Empty, id, null, 0, Start, End));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Campaign(id, CampaignType.SponsoredGame, id, Guid.Empty, null, 0, Start, End));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Campaign(id, CampaignType.SponsoredGame, id, id, null, -1, Start, End));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create((CampaignType)0));
        Assert.Throws<ArgumentException>(() =>
            new Campaign(id, CampaignType.SponsoredGame, id, id, null, 0, Start, Start));
        Assert.Throws<ArgumentException>(() => Create(CampaignType.DistrictPass));
        Assert.Throws<ArgumentException>(() => Create(district: " "));
    }

    [Fact]
    public void AvailabilityRequiresPublicationAudienceWindowAndEligibleContent()
    {
        var campaign = Create(district: " Colombo ");
        Assert.Throws<InvalidOperationException>(() => campaign.Schedule(Start));
        campaign.Schedule(Start.AddTicks(-1));

        Assert.False(campaign.IsAvailable(Start, "Colombo", true));
        Assert.Throws<InvalidOperationException>(() => campaign.Publish(Start, false));
        Assert.Equal(CampaignStatus.Scheduled, campaign.Status);

        campaign.Publish(Start.AddTicks(-1), true);
        Assert.False(campaign.IsAvailable(Start.AddTicks(-1), "Colombo", true));
        Assert.True(campaign.IsAvailable(Start, "colombo", true));
        Assert.False(campaign.IsAvailable(Start, null, true));
        Assert.False(campaign.IsAvailable(Start, "Galle", true));
        Assert.False(campaign.IsAvailable(Start, "Colombo", false));
        Assert.False(campaign.IsAvailable(End, "Colombo", true));
    }

    [Fact]
    public void PausedCampaignCanResumeBeforeExpiryAndArchiveIsTerminal()
    {
        var campaign = Create();
        Assert.Throws<InvalidOperationException>(campaign.Pause);

        campaign.Publish(Start, true);
        campaign.Pause();
        Assert.False(campaign.IsAvailable(Start, null, true));
        Assert.Throws<InvalidOperationException>(() => campaign.Publish(End, true));

        campaign.Publish(End.AddTicks(-1), true);
        Assert.True(campaign.IsAvailable(End.AddTicks(-1), null, true));

        campaign.Archive();
        campaign.Archive();
        Assert.False(campaign.IsAvailable(Start, null, true));
        Assert.Throws<InvalidOperationException>(() => campaign.Publish(Start, true));
        Assert.Throws<InvalidOperationException>(() => campaign.Schedule(Start.AddDays(-1)));
    }

    private static Campaign Create(
        CampaignType type = CampaignType.SponsoredGame,
        string? district = null)
    {
        return new Campaign(Guid.NewGuid(), type, Guid.NewGuid(), Guid.NewGuid(), district, 0,
            Start.ToOffset(TimeSpan.FromHours(5.5)), End);
    }
}
