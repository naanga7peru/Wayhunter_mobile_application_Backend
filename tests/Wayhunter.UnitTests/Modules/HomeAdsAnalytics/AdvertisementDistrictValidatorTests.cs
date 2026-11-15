
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementDistrictValidatorTests
{
    [Theory]
    [InlineData("Ampara")]
    [InlineData("Anuradhapura")]
    [InlineData("Badulla")]
    [InlineData("Batticaloa")]
    [InlineData("Colombo")]
    [InlineData("Galle")]
    [InlineData("Gampaha")]
    [InlineData("Hambantota")]
    [InlineData("Jaffna")]
    [InlineData("Kalutara")]
    [InlineData("Kandy")]
    [InlineData("Kegalle")]
    [InlineData("Kilinochchi")]
    [InlineData("Kurunegala")]
    [InlineData("Mannar")]
    [InlineData("Matale")]
    [InlineData("Matara")]
    [InlineData("Monaragala")]
    [InlineData("Mullaitivu")]
    [InlineData("Nuwara Eliya")]
    [InlineData("Polonnaruwa")]
    [InlineData("Puttalam")]
    [InlineData("Ratnapura")]
    [InlineData("Trincomalee")]
    [InlineData("Vavuniya")]
    public void ValidSriLankanDistrict_ShouldBeAccepted(
        string district)
    {
        Assert.True(
            AdvertisementDistrictValidator.IsValid(district));
    }

    [Theory]
    [InlineData("London")]
    [InlineData("NotARealDistrict")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" Colombo")]
    [InlineData("Colombo ")]
    [InlineData("Colombo1")]
    public void InvalidDistrict_ShouldBeRejected(
        string district)
    {
        Assert.False(
            AdvertisementDistrictValidator.IsValid(district));
    }

    [Fact]
    public void NullDistrict_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementDistrictValidator.IsValid(null));
    }

    [Fact]
    public void District_ShouldBeCaseInsensitive()
    {
        Assert.True(
            AdvertisementDistrictValidator.IsValid("jaffna"));

        Assert.True(
            AdvertisementDistrictValidator.IsValid("JAFFNA"));

        Assert.True(
            AdvertisementDistrictValidator.IsValid("colombo"));
    }
}
