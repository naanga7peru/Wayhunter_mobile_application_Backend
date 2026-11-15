
using System;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class NearbySearchRequestValidatorTests
{
    private static NearbySearchRequest ValidRequest() =>
        new(
            9.66,
            80.02,
            NearbyPlaceCategory.Food
        );

    [Fact]
    public void ValidRequest_ShouldPass()
    {
        NearbySearchRequestValidator.Validate(
            ValidRequest());
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public void ValidCoordinateBoundaries_ShouldPass(
        double latitude,
        double longitude)
    {
        var request = ValidRequest() with
        {
            Latitude = latitude,
            Longitude = longitude
        };

        NearbySearchRequestValidator.Validate(request);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidLatitude_ShouldFail(
        double latitude)
    {
        var request = ValidRequest() with
        {
            Latitude = latitude
        };

        Assert.Throws<ArgumentException>(() =>
            NearbySearchRequestValidator.Validate(request));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidLongitude_ShouldFail(
        double longitude)
    {
        var request = ValidRequest() with
        {
            Longitude = longitude
        };

        Assert.Throws<ArgumentException>(() =>
            NearbySearchRequestValidator.Validate(request));
    }

    [Fact]
    public void InvalidCategory_ShouldFail()
    {
        var request = ValidRequest() with
        {
            Category = (NearbyPlaceCategory)999
        };

        Assert.Throws<ArgumentException>(() =>
            NearbySearchRequestValidator.Validate(request));
    }

    [Fact]
    public void NullRequest_ShouldFail()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NearbySearchRequestValidator.Validate(null!));
    }

    [Fact]
    public void SearchRadius_ShouldBe1000Meters()
    {
        Assert.Equal(
            1000,
            NearbySearchRequestValidator.SearchRadiusMeters);
    }

    [Fact]
    public void FoodCategory_ShouldMapToRestaurant()
    {
        var result =
            NearbySearchRequestValidator
                .GetProviderSearchTerm(ValidRequest());

        Assert.Equal("restaurant", result);
    }
}
