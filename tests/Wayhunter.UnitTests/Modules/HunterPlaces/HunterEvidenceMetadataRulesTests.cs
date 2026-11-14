
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterEvidenceMetadataRulesTests
{
    private static HunterEvidence CreateValidEvidence(
        HunterEvidenceType type = HunterEvidenceType.Guided360Video)
    {
        return new HunterEvidence
        {
            HunterSubmissionId = Guid.NewGuid(),
            EvidenceType = type,
            PrivateMediaReference = "private/hunter/evidence-001",
            CapturedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            UploadedAtUtc = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void ValidGuided360Video_ShouldPass()
    {
        var evidence = CreateValidEvidence();

        HunterEvidenceMetadataRules.Validate(evidence);
    }

    [Fact]
    public void ValidLivePhoto_ShouldPass()
    {
        var evidence = CreateValidEvidence(
            HunterEvidenceType.LivePhoto);

        HunterEvidenceMetadataRules.Validate(evidence);
    }

    [Fact]
    public void NullEvidence_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HunterEvidenceMetadataRules.Validate(null!));
    }

    [Fact]
    public void EmptySubmissionId_ShouldThrow()
    {
        var evidence = CreateValidEvidence();

        evidence.HunterSubmissionId = Guid.Empty;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void InvalidEvidenceType_ShouldThrow(int value)
    {
        var evidence = CreateValidEvidence();

        evidence.EvidenceType = (HunterEvidenceType)value;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingMediaReference_ShouldThrow(
        string? mediaReference)
    {
        var evidence = CreateValidEvidence();

        evidence.PrivateMediaReference = mediaReference!;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Fact]
    public void MissingCaptureTimestamp_ShouldThrow()
    {
        var evidence = CreateValidEvidence();

        evidence.CapturedAtUtc = default;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Fact]
    public void MissingUploadTimestamp_ShouldThrow()
    {
        var evidence = CreateValidEvidence();

        evidence.UploadedAtUtc = default;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Fact]
    public void LatitudeWithoutLongitude_ShouldThrow()
    {
        var evidence = CreateValidEvidence();

        evidence.CaptureLatitude = 9.6615;
        evidence.CaptureLongitude = null;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Fact]
    public void LongitudeWithoutLatitude_ShouldThrow()
    {
        var evidence = CreateValidEvidence();

        evidence.CaptureLatitude = null;
        evidence.CaptureLongitude = 80.0087;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    [InlineData(double.NaN)]
    public void InvalidLatitude_ShouldThrow(double latitude)
    {
        var evidence = CreateValidEvidence();

        evidence.CaptureLatitude = latitude;
        evidence.CaptureLongitude = 80.0087;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidLongitude_ShouldThrow(double longitude)
    {
        var evidence = CreateValidEvidence();

        evidence.CaptureLatitude = 9.6615;
        evidence.CaptureLongitude = longitude;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDuration_ShouldThrow(double duration)
    {
        var evidence = CreateValidEvidence();

        evidence.DurationSeconds = duration;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterEvidenceMetadataRules.Validate(evidence));
    }

    [Fact]
    public void ValidGpsAndDuration_ShouldPass()
    {
        var evidence = CreateValidEvidence();

        evidence.CaptureLatitude = 9.6615;
        evidence.CaptureLongitude = 80.0087;
        evidence.DurationSeconds = 30;

        HunterEvidenceMetadataRules.Validate(evidence);
    }
}
