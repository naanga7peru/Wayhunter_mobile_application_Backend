
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Domain.Modules.HunterPlaces.Rules;

public static class HunterEvidenceCaptureOrderRules
{
    public static void ValidateVideoBeforePhoto(
        HunterEvidence video,
        HunterEvidence photo)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(photo);

        // Reuse existing metadata validation.
        HunterEvidenceMetadataRules.Validate(video);
        HunterEvidenceMetadataRules.Validate(photo);

        // Check correct evidence types.
        if (video.EvidenceType != HunterEvidenceType.Guided360Video)
        {
            throw new ArgumentException(
                "First evidence must be a guided 360-degree video.",
                nameof(video));
        }

        if (photo.EvidenceType != HunterEvidenceType.LivePhoto)
        {
            throw new ArgumentException(
                "Second evidence must be a live photo.",
                nameof(photo));
        }

        // Both must belong to the same Hunter submission.
        if (video.HunterSubmissionId != photo.HunterSubmissionId)
        {
            throw new InvalidOperationException(
                "Video and photo must belong to the same Hunter submission.");
        }

        // Capture must not occur after the recorded upload.
        if (video.CapturedAtUtc > video.UploadedAtUtc ||
            photo.CapturedAtUtc > photo.UploadedAtUtc)
        {
            throw new InvalidOperationException(
                "Evidence capture time cannot be after upload time.");
        }

        // The video must have been uploaded before photo capture.
        if (video.UploadedAtUtc >= photo.CapturedAtUtc)
        {
            throw new InvalidOperationException(
                "Guided 360-degree video must be uploaded " +
                "before the live photo is captured.");
        }
    }
}
