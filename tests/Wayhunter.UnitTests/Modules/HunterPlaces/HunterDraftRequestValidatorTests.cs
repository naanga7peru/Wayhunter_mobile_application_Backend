using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Validators;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterDraftRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static HunterDraftRequestDto Valid() => new()
    { PlaceName = "  Lake View ", ClaimedDistrict = " Colombo ", Latitude = 6.9, Longitude = 79.8 };

    [Fact]
    public void ValidRequestTrimsFieldsAndAppliesOptionalValues()
    {
        var request = Valid();
        request.Description = " Description "; request.GpsAccuracyMeters = 4.2;
        request.LocationCapturedAtUtc = Now.AddMinutes(-1);
        Assert.Empty(HunterDraftRequestValidator.Validate(request, Now));
        var submission = new HunterSubmission();
        HunterDraftRequestValidator.Apply(request, submission);
        Assert.Equal("Lake View", submission.PlaceName);
        Assert.Equal("Colombo", submission.District);
        Assert.Equal("Description", submission.Description);
        Assert.Equal(4.2, submission.GpsAccuracyMeters);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    public void InvalidCoordinatesRejected(double latitude, double longitude)
    {
        var request = Valid(); request.Latitude = latitude; request.Longitude = longitude;
        Assert.NotEmpty(HunterDraftRequestValidator.Validate(request, Now));
    }

    [Fact]
    public void NonPositiveAccuracyAndFutureCaptureRejected()
    {
        var request = Valid(); request.GpsAccuracyMeters = 0;
        request.LocationCapturedAtUtc = Now.AddMinutes(6);
        Assert.Equal(2, HunterDraftRequestValidator.Validate(request, Now).Count);
    }
}
