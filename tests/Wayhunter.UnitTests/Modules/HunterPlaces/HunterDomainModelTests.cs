
using System;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public class HunterDomainModelTests
{
    [Fact]
    public void NewSubmission_ShouldStartAsDraft()
    {
        var submission = new HunterSubmission();

        Assert.NotEqual(Guid.Empty, submission.Id);
        Assert.Equal(
            HunterSubmissionStatus.Draft,
            submission.Status
        );
        Assert.Null(submission.SubmittedAtUtc);
    }

    [Fact]
    public void NewSubmissions_ShouldHaveUniqueIds()
    {
        var first = new HunterSubmission();
        var second = new HunterSubmission();

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Evidence_ShouldHaveUniqueId()
    {
        var evidence = new HunterEvidence();

        Assert.NotEqual(Guid.Empty, evidence.Id);
        Assert.Equal(
            string.Empty,
            evidence.PrivateMediaReference
        );
    }

    [Fact]
    public void ReviewDecision_ShouldHaveUniqueId()
    {
        var review = new HunterReviewDecision();

        Assert.NotEqual(Guid.Empty, review.Id);
    }

    [Fact]
    public void EvidenceType_ShouldIncludeVideoAndPhoto()
    {
        Assert.NotEqual(
            HunterEvidenceType.Guided360Video,
            HunterEvidenceType.LivePhoto
        );
    }
}
