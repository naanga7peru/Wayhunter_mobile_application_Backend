
using System;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class NearbyPlaceCategoryMapperTests
{
    [Theory]
    [InlineData("food", NearbyPlaceCategory.Food)]
    [InlineData("hospital", NearbyPlaceCategory.Hospital)]
    [InlineData("fuel", NearbyPlaceCategory.Fuel)]
    [InlineData("police", NearbyPlaceCategory.Police)]
    [InlineData("atm", NearbyPlaceCategory.Atm)]
    [InlineData("attraction", NearbyPlaceCategory.Attraction)]
    public void ValidClientCategory_ShouldMapCorrectly(
        string value,
        NearbyPlaceCategory expected)
    {
        var actual =
            NearbyPlaceCategoryMapper.FromClientValue(value);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(NearbyPlaceCategory.Food, "restaurant")]
    [InlineData(NearbyPlaceCategory.Hospital, "hospital")]
    [InlineData(NearbyPlaceCategory.Fuel, "fuel station")]
    [InlineData(NearbyPlaceCategory.Police, "police station")]
    [InlineData(NearbyPlaceCategory.Atm, "ATM")]
    [InlineData(NearbyPlaceCategory.Attraction, "tourist attraction")]
    public void ValidCategory_ShouldReturnSearchTerm(
        NearbyPlaceCategory category,
        string expected)
    {
        var actual =
            NearbyPlaceCategoryMapper.ToSearchTerm(category);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void UppercaseCategory_ShouldWork()
    {
        var result =
            NearbyPlaceCategoryMapper.FromClientValue("FOOD");

        Assert.Equal(NearbyPlaceCategory.Food, result);
    }

    [Fact]
    public void CategoryWithSpaces_ShouldWork()
    {
        var result =
            NearbyPlaceCategoryMapper.FromClientValue(
                "  hospital  ");

        Assert.Equal(NearbyPlaceCategory.Hospital, result);
    }

    [Fact]
    public void EmptyCategory_ShouldFail()
    {
        Assert.Throws<ArgumentException>(() =>
            NearbyPlaceCategoryMapper.FromClientValue(""));
    }

    [Fact]
    public void WhitespaceCategory_ShouldFail()
    {
        Assert.Throws<ArgumentException>(() =>
            NearbyPlaceCategoryMapper.FromClientValue("   "));
    }

    [Fact]
    public void UnknownCategory_ShouldFail()
    {
        Assert.Throws<ArgumentException>(() =>
            NearbyPlaceCategoryMapper.FromClientValue(
                "unknown-category"));
    }

    [Fact]
    public void InvalidEnum_ShouldFail()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NearbyPlaceCategoryMapper.ToSearchTerm(
                (NearbyPlaceCategory)999));
    }
}
