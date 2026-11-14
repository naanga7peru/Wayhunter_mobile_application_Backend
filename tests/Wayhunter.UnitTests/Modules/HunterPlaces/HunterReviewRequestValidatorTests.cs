
using System;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public class HunterReviewRequestValidatorTests
{
    [Fact]
    public void Approved_WithValidId_ShouldPass()
    {
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = Guid.NewGuid(),
            ResultingStatus = HunterSubmissionStatus.Approved
        };

        var errors = HunterReviewRequestValidator.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejected_WithoutReason_ShouldFail()
    {
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = Guid.NewGuid(),
            ResultingStatus = HunterSubmissionStatus.Rejected
        };

        var errors = HunterReviewRequestValidator.Validate(request);

        Assert.Contains(errors,
            error => error.Contains("reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Held_WithoutReason_ShouldFail()
    {
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = Guid.NewGuid(),
            ResultingStatus = HunterSubmissionStatus.Held
        };

        var errors = HunterReviewRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.Draft)]
    [InlineData(HunterSubmissionStatus.PendingReview)]
    public void InvalidAdminDecision_ShouldFail(
        HunterSubmissionStatus status)
    {
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = Guid.NewGuid(),
            ResultingStatus = status
        };

        var errors = HunterReviewRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void EmptySubmissionId_ShouldFail()
    {
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = Guid.Empty,
            ResultingStatus = HunterSubmissionStatus.Approved
        };

        var errors = HunterReviewRequestValidator.Validate(request);

        Assert.NotEmpty(errors);
    }
}
