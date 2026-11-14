
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterEvidenceCaptureOrderRulesTests
{
    private static readonly DateTimeOffset BaseTime =
        new DateTimeOffset(
            2026, 11, 14, 5, 0, 0, TimeSpan.Zero);

    private static (HunterEvidence Video, HunterEvidence Photo)
        CreateValidEvidencePair()
    {
        var submissionId = Guid.NewGuid();

        var video = new HunterEvidence
        {
            HunterSubmissionId = submissionId,
            EvidenceType = HunterEvidenceType.Guided360Video,
            PrivateMediaReference = "private/hunter/video-001",
            CapturedAtUtc = BaseTime,
            UploadedAtUtc = BaseTime.AddSeconds(30),
            DurationSeconds = 20
        };

        var photo = new HunterEvidence
        {
            HunterSubmissionId = submissionId,
            EvidenceType = HunterEvidenceType.LivePhoto,
            PrivateMediaReference = "private/hunter/photo-001",
            CapturedAtUtc = BaseTime.AddMinutes(1),
            UploadedAtUtc = BaseTime.AddMinutes(2)
        };

        return (video, photo);
    }

    [Fact]
    public void ValidVideoBeforePhoto_ShouldPass()
    {
        var (video, photo) = CreateValidEvidencePair();

        HunterEvidenceCaptureOrderRules
            .ValidateVideoBeforePhoto(video, photo);
    }

    [Fact]
    public void PhotoCapturedBeforeVideoUpload_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        photo.CapturedAtUtc =
            video.UploadedAtUtc.AddSeconds(-1);

        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void PhotoCapturedAtVideoUploadTime_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        photo.CapturedAtUtc = video.UploadedAtUtc;

        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void DifferentSubmissionIds_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        photo.HunterSubmissionId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void WrongVideoEvidenceType_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        video.EvidenceType = HunterEvidenceType.LivePhoto;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void WrongPhotoEvidenceType_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        photo.EvidenceType =
            HunterEvidenceType.Guided360Video;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void VideoCapturedAfterUpload_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        video.CapturedAtUtc =
            video.UploadedAtUtc.AddSeconds(1);

        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void PhotoCapturedAfterUpload_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        photo.CapturedAtUtc =
            photo.UploadedAtUtc.AddSeconds(1);

        Assert.Throws<InvalidOperationException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void MissingVideoMediaReference_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        video.PrivateMediaReference = "";

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void MissingPhotoCaptureTimestamp_ShouldThrow()
    {
        var (video, photo) = CreateValidEvidencePair();

        photo.CapturedAtUtc = default;

        Assert.Throws<ArgumentException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, photo));
    }

    [Fact]
    public void NullVideo_ShouldThrow()
    {
        var (_, photo) = CreateValidEvidencePair();

        Assert.Throws<ArgumentNullException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(null!, photo));
    }

    [Fact]
    public void NullPhoto_ShouldThrow()
    {
        var (video, _) = CreateValidEvidencePair();

        Assert.Throws<ArgumentNullException>(() =>
            HunterEvidenceCaptureOrderRules
                .ValidateVideoBeforePhoto(video, null!));
    }
}
