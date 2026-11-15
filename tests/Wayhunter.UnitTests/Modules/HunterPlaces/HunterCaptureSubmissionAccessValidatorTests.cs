
using System;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterCaptureSubmissionAccessValidatorTests
{
    private static HunterCaptureSubmissionAccessSnapshot CreateSnapshot(
        Guid submissionId,
        string ownerUserId = "hunter-001")
    {
        return new HunterCaptureSubmissionAccessSnapshot
        {
            SubmissionId = submissionId,
            OwnerUserId = ownerUserId
        };
    }

    [Fact]
    public void CorrectOwner_ShouldAllowAccess()
    {
        var submissionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(submissionId);

        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            "hunter-001",
            submissionId,
            snapshot);

        Assert.True(result);
    }

    [Fact]
    public void DifferentOwner_ShouldDenyAccess()
    {
        var submissionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(submissionId);

        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            "hunter-002",
            submissionId,
            snapshot);

        Assert.False(result);
    }

    [Fact]
    public void MissingSnapshot_ShouldDenyAccess()
    {
        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            "hunter-001",
            Guid.NewGuid(),
            null);

        Assert.False(result);
    }

    [Fact]
    public void EmptySubmissionId_ShouldDenyAccess()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid());

        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            "hunter-001",
            Guid.Empty,
            snapshot);

        Assert.False(result);
    }

    [Fact]
    public void MismatchedSubmissionId_ShouldDenyAccess()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid());

        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            "hunter-001",
            Guid.NewGuid(),
            snapshot);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingAuthenticatedUserId_ShouldDenyAccess(
        string? authenticatedUserId)
    {
        var submissionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(submissionId);

        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            authenticatedUserId,
            submissionId,
            snapshot);

        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingStoredOwner_ShouldDenyAccess(
        string ownerUserId)
    {
        var submissionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(
            submissionId,
            ownerUserId);

        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            "hunter-001",
            submissionId,
            snapshot);

        Assert.False(result);
    }

    [Fact]
    public void DifferentCase_ShouldDenyAccess()
    {
        var submissionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(
            submissionId,
            "hunter-001");

        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            "HUNTER-001",
            submissionId,
            snapshot);

        Assert.False(result);
    }

    [Fact]
    public void ExtraWhitespace_ShouldDenyAccess()
    {
        var submissionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(submissionId);

        var result = HunterCaptureSubmissionAccessValidator.CanAccess(
            " hunter-001 ",
            submissionId,
            snapshot);

        Assert.False(result);
    }
}
