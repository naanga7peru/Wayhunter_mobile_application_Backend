
using System;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterSubmitRequestValidatorTests
{
    private static HunterSubmitRequestDto CreateValidRequest()
    {
        return new HunterSubmitRequestDto
        {
            PlaceName = "Jaffna Fort",
            Description = "Historical place",
            ClaimedDistrict = "Jaffna",
            Latitude = 9.6615,
            Longitude = 80.0087,
            GpsAccuracyMeters = 10,
            LocationCapturedAtUtc = DateTimeOffset.UtcNow,
            Guided360VideoEvidenceId = Guid.NewGuid(),
            LivePhotoEvidenceId = Guid.NewGuid()
        };
    }

    [Fact]
    public void ValidRequest_ShouldPass()
    {
        var request = CreateValidRequest();

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void EmptyPlaceName_ShouldFail()
    {
        var request = CreateValidRequest();
        request.PlaceName = "";

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void EmptyDistrict_ShouldFail()
    {
        var request = CreateValidRequest();
        request.ClaimedDistrict = "";

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    [InlineData(double.NaN)]
    public void InvalidLatitude_ShouldFail(double latitude)
    {
        var request = CreateValidRequest();
        request.Latitude = latitude;

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidLongitude_ShouldFail(double longitude)
    {
        var request = CreateValidRequest();
        request.Longitude = longitude;

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void InvalidGpsAccuracy_ShouldFail(double accuracy)
    {
        var request = CreateValidRequest();
        request.GpsAccuracyMeters = accuracy;

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Missing360Video_ShouldFail()
    {
        var request = CreateValidRequest();
        request.Guided360VideoEvidenceId = Guid.Empty;

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void MissingLivePhoto_ShouldFail()
    {
        var request = CreateValidRequest();
        request.LivePhotoEvidenceId = Guid.Empty;

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void SameVideoAndPhotoId_ShouldFail()
    {
        var request = CreateValidRequest();

        request.LivePhotoEvidenceId =
            request.Guided360VideoEvidenceId;

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidGpsBoundaries_ShouldPass()
    {
        var request = CreateValidRequest();

        request.Latitude = -90;
        request.Longitude = 180;

        var errors =
            HunterSubmitRequestValidator.Validate(request);

        Assert.Empty(errors);
    }
}
