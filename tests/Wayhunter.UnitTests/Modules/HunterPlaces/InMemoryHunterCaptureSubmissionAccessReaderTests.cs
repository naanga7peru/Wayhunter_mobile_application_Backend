
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class InMemoryHunterCaptureSubmissionAccessReaderTests
{
    private static HunterSubmission CreateSubmission(
        Guid? submissionId = null,
        string userId = "hunter-001",
        HunterSubmissionStatus status = HunterSubmissionStatus.Draft)
    {
        return new HunterSubmission
        {
            Id = submissionId ?? Guid.NewGuid(),
            UserId = userId,
            PlaceName = "Test Hunter Place",
            District = "Jaffna",
            Status = status
        };
    }

    [Fact]
    public void NullCollection_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InMemoryHunterCaptureSubmissionAccessReader(
                null!));
    }

    [Fact]
    public async Task EmptyCollection_ShouldReturnNull()
    {
        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                Array.Empty<HunterSubmission>());

        var result = await reader.FindBySubmissionIdAsync(
            Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ExistingSubmission_ShouldReturnSnapshot()
    {
        var submissionId = Guid.NewGuid();

        var submission = CreateSubmission(
            submissionId,
            "hunter-001",
            HunterSubmissionStatus.PendingReview);

        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { submission });

        var result = await reader.FindBySubmissionIdAsync(
            submissionId);

        Assert.NotNull(result);

        Assert.Equal(
            submissionId,
            result.SubmissionId);

        Assert.Equal(
            "hunter-001",
            result.OwnerUserId);

        Assert.Equal(
            HunterSubmissionStatus.PendingReview,
            result.Status);
    }

    [Fact]
    public async Task UnknownSubmission_ShouldReturnNull()
    {
        var submission = CreateSubmission();

        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { submission });

        var result = await reader.FindBySubmissionIdAsync(
            Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldReturnNull()
    {
        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { CreateSubmission() });

        var result = await reader.FindBySubmissionIdAsync(
            Guid.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void EmptyStoredSubmissionId_ShouldThrow()
    {
        var submission = CreateSubmission(Guid.Empty);

        Assert.Throws<ArgumentException>(() =>
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { submission }));
    }

    [Fact]
    public void DuplicateSubmissionIds_ShouldThrow()
    {
        var submissionId = Guid.NewGuid();

        var first = CreateSubmission(
            submissionId,
            "hunter-001");

        var second = CreateSubmission(
            submissionId,
            "hunter-002");

        Assert.Throws<ArgumentException>(() =>
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { first, second }));
    }

    [Fact]
    public void NullSubmissionItem_ShouldThrow()
    {
        var submissions = new HunterSubmission[]
        {
            CreateSubmission(),
            null!
        };

        Assert.Throws<ArgumentException>(() =>
            new InMemoryHunterCaptureSubmissionAccessReader(
                submissions));
    }

    [Fact]
    public async Task Snapshot_ShouldNotChangeAfterSourceMutation()
    {
        var submissionId = Guid.NewGuid();

        var submission = CreateSubmission(
            submissionId,
            "hunter-001",
            HunterSubmissionStatus.Draft);

        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { submission });

        // Modify the original mutable entity.
        submission.UserId = "hunter-002";
        submission.Status =
            HunterSubmissionStatus.Approved;

        var result = await reader.FindBySubmissionIdAsync(
            submissionId);

        Assert.NotNull(result);

        // Reader must preserve its original snapshot.
        Assert.Equal(
            "hunter-001",
            result.OwnerUserId);

        Assert.Equal(
            HunterSubmissionStatus.Draft,
            result.Status);
    }

    [Fact]
    public async Task CancelledLookup_ShouldThrow()
    {
        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { CreateSubmission() });

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.FindBySubmissionIdAsync(
                Guid.NewGuid(),
                cts.Token));
    }

    [Fact]
    public async Task MultipleSubmissions_ShouldFindCorrectOwner()
    {
        var first = CreateSubmission(
            userId: "hunter-001");

        var second = CreateSubmission(
            userId: "hunter-002");

        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                new[] { first, second });

        var result = await reader.FindBySubmissionIdAsync(
            second.Id);

        Assert.NotNull(result);

        Assert.Equal(
            second.Id,
            result.SubmissionId);

        Assert.Equal(
            "hunter-002",
            result.OwnerUserId);
    }
}
