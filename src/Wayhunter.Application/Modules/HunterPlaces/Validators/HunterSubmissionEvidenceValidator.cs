using System;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Rules;

namespace Wayhunter.Application.Modules.HunterPlaces.Validation;

/// <summary>Checks evidence records loaded by the backend for an owned submission.</summary>
public static class HunterSubmissionEvidenceValidator
{
    public static void EnsureReady(
        Guid submissionId, HunterSubmitRequestDto request,
        HunterEvidence? video, HunterEvidence? photo)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (submissionId == Guid.Empty ||
            request.Guided360VideoEvidenceId == Guid.Empty ||
            request.LivePhotoEvidenceId == Guid.Empty ||
            request.Guided360VideoEvidenceId == request.LivePhotoEvidenceId ||
            video is null || photo is null ||
            video.Id != request.Guided360VideoEvidenceId ||
            photo.Id != request.LivePhotoEvidenceId ||
            video.HunterSubmissionId != submissionId ||
            photo.HunterSubmissionId != submissionId)
        {
            throw new InvalidOperationException(
                "Both evidence records must match the Hunter submission.");
        }

        HunterEvidenceCaptureOrderRules.ValidateVideoBeforePhoto(video, photo);
    }
}
