
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterEvidenceSequenceRulesTests
{
    private static readonly Guid SubmissionId = Guid.NewGuid();

    private static HunterEvidence CreateEvidence(
        HunterEvidenceType type,
        string mediaReference,
        Guid? submissionId = null)
    {
        return new HunterEvidence
        {
            HunterSubmissionId = submissionId ?? SubmissionId,
            EvidenceType = type,
            PrivateMediaReference = mediaReference
        };
    }

    [Fact]
    public void Guided360Video_ShouldBeAllowedFirst()
    {
        HunterEvidenceSequenceRules.ValidateNextEvidence(
            SubmissionId,
            HunterEvidenceType.Guided360Video,
            Array.Empty<HunterEvidence>());
    }

    [Fact]
    public void LivePhoto_Without360Video_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceSequenceRules.ValidateNextEvidence(
                SubmissionId,
                HunterEvidenceType.LivePhoto,
                Array.Empty<HunterEvidence>()));
    }

    [Fact]
    public void LivePhoto_WithOnlyPreviousPhoto_ShouldThrow()
    {
        var evidence = new[]
        {
            CreateEvidence(
                HunterEvidenceType.LivePhoto,
                "private/photo-001")
        };

        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceSequenceRules.ValidateNextEvidence(
                SubmissionId,
                HunterEvidenceType.LivePhoto,
                evidence));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LivePhoto_WithMissing360Media_ShouldThrow(
        string mediaReference)
    {
        var evidence = new[]
        {
            CreateEvidence(
                HunterEvidenceType.Guided360Video,
                mediaReference)
        };

        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceSequenceRules.ValidateNextEvidence(
                SubmissionId,
                HunterEvidenceType.LivePhoto,
                evidence));
    }

    [Fact]
    public void LivePhoto_WithUploaded360Video_ShouldBeAllowed()
    {
        var evidence = new[]
        {
            CreateEvidence(
                HunterEvidenceType.Guided360Video,
                "private/360-video-001")
        };

        HunterEvidenceSequenceRules.ValidateNextEvidence(
            SubmissionId,
            HunterEvidenceType.LivePhoto,
            evidence);
    }

    [Fact]
    public void LivePhoto_WithMultipleEvidence_ShouldBeAllowed()
    {
        var evidence = new[]
        {
            CreateEvidence(
                HunterEvidenceType.Guided360Video,
                "private/360-video-001"),

            CreateEvidence(
                HunterEvidenceType.LivePhoto,
                "private/photo-001")
        };

        HunterEvidenceSequenceRules.ValidateNextEvidence(
            SubmissionId,
            HunterEvidenceType.LivePhoto,
            evidence);
    }

    [Fact]
    public void VideoFromAnotherSubmission_ShouldNotAllowPhoto()
    {
        var anotherSubmissionId = Guid.NewGuid();

        var evidence = new[]
        {
            CreateEvidence(
                HunterEvidenceType.Guided360Video,
                "private/other-360-video",
                anotherSubmissionId)
        };

        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceSequenceRules.ValidateNextEvidence(
                SubmissionId,
                HunterEvidenceType.LivePhoto,
                evidence));
    }

    [Fact]
    public void EmptySubmissionId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceSequenceRules.ValidateNextEvidence(
                Guid.Empty,
                HunterEvidenceType.LivePhoto,
                Array.Empty<HunterEvidence>()));
    }

    [Fact]
    public void NullPreviousEvidence_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HunterEvidenceSequenceRules.ValidateNextEvidence(
                SubmissionId,
                HunterEvidenceType.LivePhoto,
                null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void InvalidEvidenceType_ShouldThrow(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HunterEvidenceSequenceRules.ValidateNextEvidence(
                SubmissionId,
                (HunterEvidenceType)value,
                Array.Empty<HunterEvidence>()));
    }
}
