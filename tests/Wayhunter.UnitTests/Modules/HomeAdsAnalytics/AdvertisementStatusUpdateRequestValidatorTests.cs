
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementStatusUpdateRequestValidatorTests
{
    [Theory]
    [InlineData(AdvertisementStatus.Active)]
    [InlineData(AdvertisementStatus.Paused)]
    [InlineData(AdvertisementStatus.Archived)]
    public void ValidStatus_ShouldBeAccepted(
        AdvertisementStatus status)
    {
        var request = new AdvertisementStatusUpdateRequest
        {
            Status = status
        };

        Assert.True(
            AdvertisementStatusUpdateRequestValidator.IsValid(
                request));
    }

    [Fact]
    public void NullRequest_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementStatusUpdateRequestValidator.IsValid(
                null));
    }

    [Fact]
    public void MissingStatus_ShouldBeRejected()
    {
        var request = new AdvertisementStatusUpdateRequest
        {
            Status = null
        };

        Assert.False(
            AdvertisementStatusUpdateRequestValidator.IsValid(
                request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void InvalidStatus_ShouldBeRejected(int status)
    {
        var request = new AdvertisementStatusUpdateRequest
        {
            Status = (AdvertisementStatus)status
        };

        Assert.False(
            AdvertisementStatusUpdateRequestValidator.IsValid(
                request));
    }
}
