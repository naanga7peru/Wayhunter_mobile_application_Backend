
using System;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterStartCaptureSessionRequestValidatorTests
{
    [Fact]
    public void ValidSubmissionId_ShouldPass()
    {
        var request = new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = Guid.NewGuid()
        };

        var errors =
            HunterStartCaptureSessionRequestValidator.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void EmptySubmissionId_ShouldFail()
    {
        var request = new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = Guid.Empty
        };

        var errors =
            HunterStartCaptureSessionRequestValidator.Validate(request);

        Assert.Contains(errors, error =>
            error.Contains(
                "Hunter submission ID",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NullRequest_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            HunterStartCaptureSessionRequestValidator.Validate(null!));
    }
}
