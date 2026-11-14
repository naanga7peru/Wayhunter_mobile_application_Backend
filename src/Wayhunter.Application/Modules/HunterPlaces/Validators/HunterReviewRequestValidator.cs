
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Validation;

public static class HunterReviewRequestValidator
{
    public static IReadOnlyList<string> Validate(
        HunterReviewRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        // A submission must be selected.
        if (request.HunterSubmissionId == Guid.Empty)
        {
            errors.Add("Hunter submission ID is required.");
        }

        // Only these statuses are valid Admin decisions.
        bool validDecision =
            request.ResultingStatus is
                HunterSubmissionStatus.Approved
                or HunterSubmissionStatus.Rejected
                or HunterSubmissionStatus.Held;

        if (!validDecision)
        {
            errors.Add("Invalid Admin review decision.");
        }

        // Proposed validation policy:
        // Rejected and Held decisions require a reason.
        bool reasonRequired =
            request.ResultingStatus is
                HunterSubmissionStatus.Rejected
                or HunterSubmissionStatus.Held;

        if (reasonRequired &&
            string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add(
                "A reason is required for rejection or hold.");
        }

        return errors;
    }
}
