
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterCaptureSubmissionOwnershipServiceTests
{
    private sealed class FakeSubmissionAccessReader
        : IHunterCaptureSubmissionAccessReader
    {
        public HunterCaptureSubmissionAccessSnapshot? Snapshot
        {
            get;
            set;
        }

        public int ReadCount { get; private set; }

        public Guid LastSubmissionId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<HunterCaptureSubmissionAccessSnapshot?>
            FindBySubmissionIdAsync(
                Guid submissionId,
                CancellationToken cancellationToken = default)
        {
            ReadCount++;
            LastSubmissionId = submissionId;
            LastToken = cancellationToken;

            return Task.FromResult(Snapshot);
        }
    }

    private static (
        HunterCaptureSubmissionOwnershipService Service,
        FakeSubmissionAccessReader Reader)
        CreateService()
    {
        var reader = new FakeSubmissionAccessReader();

        var service =
            new HunterCaptureSubmissionOwnershipService(reader);

        return (service, reader);
    }

    private static HunterCaptureSubmissionAccessSnapshot
        CreateSnapshot(
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
    public void NullReader_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HunterCaptureSubmissionOwnershipService(null!));
    }

    [Fact]
    public async Task CorrectOwner_ShouldAllowAccess()
    {
        var (service, reader) = CreateService();

        var submissionId = Guid.NewGuid();

        reader.Snapshot = CreateSnapshot(submissionId);

        await service.EnsureOwnedAsync(
            "hunter-001",
            submissionId);

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(submissionId, reader.LastSubmissionId);
    }

    [Fact]
    public async Task DifferentOwner_ShouldDenyAccess()
    {
        var (service, reader) = CreateService();

        var submissionId = Guid.NewGuid();

        reader.Snapshot = CreateSnapshot(
            submissionId,
            "hunter-001");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.EnsureOwnedAsync(
                "hunter-002",
                submissionId));

        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task MissingSubmission_ShouldDenyAccess()
    {
        var (service, reader) = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.EnsureOwnedAsync(
                "hunter-001",
                Guid.NewGuid()));

        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task MismatchedSubmission_ShouldDenyAccess()
    {
        var (service, reader) = CreateService();

        reader.Snapshot = CreateSnapshot(Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.EnsureOwnedAsync(
                "hunter-001",
                Guid.NewGuid()));

        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task MissingStoredOwner_ShouldDenyAccess()
    {
        var (service, reader) = CreateService();

        var submissionId = Guid.NewGuid();

        reader.Snapshot = CreateSnapshot(
            submissionId,
            "");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.EnsureOwnedAsync(
                "hunter-001",
                submissionId));

        Assert.Equal(1, reader.ReadCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingAuthenticatedUserId_ShouldDenyAccess(
        string? authenticatedUserId)
    {
        var (service, reader) = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.EnsureOwnedAsync(
                authenticatedUserId,
                Guid.NewGuid()));

        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldDenyAccess()
    {
        var (service, reader) = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.EnsureOwnedAsync(
                "hunter-001",
                Guid.Empty));

        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotRead()
    {
        var (service, reader) = CreateService();

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.EnsureOwnedAsync(
                "hunter-001",
                Guid.NewGuid(),
                cts.Token));

        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task CancellationToken_ShouldBeForwarded()
    {
        var (service, reader) = CreateService();

        var submissionId = Guid.NewGuid();

        reader.Snapshot = CreateSnapshot(submissionId);

        using var cts = new CancellationTokenSource();

        await service.EnsureOwnedAsync(
            "hunter-001",
            submissionId,
            cts.Token);

        Assert.Equal(cts.Token, reader.LastToken);
        Assert.Equal(1, reader.ReadCount);
    }
}
