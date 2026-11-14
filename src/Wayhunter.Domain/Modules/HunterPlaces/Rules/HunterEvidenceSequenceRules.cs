
using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Domain.Modules.HunterPlaces.Rules;

public static class HunterEvidenceSequenceRules
{
    public static void ValidateNextEvidence(
        Guid hunterSubmissionId,
        HunterEvidenceType nextType,
        IEnumerable<HunterEvidence> previousEvidence)
    {
        if (hunterSubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission ID is required.",
                nameof(hunterSubmissionId));
        }

        ArgumentNullException.ThrowIfNull(previousEvidence);

        if (!Enum.IsDefined(nextType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextType));
        }

        // The guided 360-degree video must come first.
        if (nextType == HunterEvidenceType.Guided360Video)
        {
            return;
        }

        // A photo requires a previously uploaded 360 video
        // belonging to this exact Hunter submission.
        bool hasUploaded360Video = previousEvidence.Any(
            evidence =>
                evidence is not null &&
                evidence.HunterSubmissionId == hunterSubmissionId &&
                evidence.EvidenceType ==
                    HunterEvidenceType.Guided360Video &&
                !string.IsNullOrWhiteSpace(
                    evidence.PrivateMediaReference));

        if (!hasUploaded360Video)
        {
            throw new InvalidOperationException(
                "A guided 360-degree video for this Hunter " +
                "submission must be uploaded before live photo evidence.");
        }
    }
}
