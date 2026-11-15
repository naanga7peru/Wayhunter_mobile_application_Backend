
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Validation;

public static class HunterStartCaptureSessionRequestValidator
{
    public static IReadOnlyList<string> Validate(
        HunterStartCaptureSessionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();

        if (request.HunterSubmissionId == Guid.Empty)
        {
            errors.Add("Hunter submission ID is required.");
        }

        return errors;
    }
}
