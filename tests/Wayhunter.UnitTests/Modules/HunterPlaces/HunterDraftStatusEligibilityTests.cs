
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterDraftStatusEligibilityTests
{
    private const string HunterA = "hunter-001";
    private const string HunterB = "hunter-002";

    private sealed class FakeIdentityResolver
        : IHunterCaptureSessionIdentityResolver
    {
        public Guid HunterId { get; } = Guid.NewGuid();

        public int ResolveCount { get; private set; }

        public Task<Guid?> ResolveSessionHunterIdAsync(
            string authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResolveCount++;

            return Task.FromResult<Guid?>(HunterId);
        }
    }

    private sealed class FakeSessionStore
        : IHunterCaptureSessionStore
    {
        public Guid SessionId { get; } = Guid.NewGuid();

        public int CreateCount { get; private set; }

        public Guid LastSubmissionId { get; private set; }

        public Task<Guid> CreateAsync(
            Guid hunterId,
            Guid hunterSubmissionId,
            DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CreateCount++;
            LastSubmissionId = hunterSubmissionId;

            return Task.FromResult(SessionId);
        }

        public Task<bool> TryConsumeAsync(
            Guid captureSessionId,
            Guid authenticatedHunterId,
            Guid hunterSubmissionId,
            DateTimeOffset serverReceivedAtUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private static HunterSubmission CreateSubmission(
        string ownerUserId,
        HunterSubmissionStatus status)
    {
        return new HunterSubmission
        {
            Id = Guid.NewGuid(),
            UserId = ownerUserId,
            PlaceName = "Test Location",
            District = "Jaffna",
            Status = status
        };
    }

    private static HunterStartCaptureSessionRequestDto
        CreateRequest(Guid submissionId)
    {
        return new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = submissionId
        };
    }

    private static (
        HunterStartAuthorizedCaptureSessionHandler Handler,
        FakeIdentityResolver Resolver,
        FakeSessionStore Store)
        CreateHandler(params HunterSubmission[] submissions)
    {
        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                submissions);

        var ownershipService =
            new HunterCaptureSubmissionOwnershipService(reader);

        var resolver = new FakeIdentityResolver();

        var identityService =
            new HunterCaptureSessionIdentityResolutionService(
                resolver);

        var store = new FakeSessionStore();

        var sessionService =
            new HunterCaptureSessionService(
                store,
                TimeProvider.System);

        var captureHandler =
            new HunterStartCaptureSessionHandler(
                sessionService);

        var handler =
            new HunterStartAuthorizedCaptureSessionHandler(
                ownershipService,
                identityService,
                captureHandler);

        return (handler, resolver, store);
    }

    [Fact]
    public async Task Draft_ShouldAllowNewCaptureSession()
    {
        var submission = CreateSubmission(
            HunterA,
            HunterSubmissionStatus.Draft);

        var (handler, resolver, store) =
            CreateHandler(submission);

        var response = await handler.HandleAsync(
            HunterA,
            CreateRequest(submission.Id));

        Assert.Equal(store.SessionId, response.CaptureSessionId);
        Assert.Equal(submission.Id, response.HunterSubmissionId);
        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal(1, store.CreateCount);
        Assert.Equal(submission.Id, store.LastSubmissionId);
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.PendingReview)]
    [InlineData(HunterSubmissionStatus.Held)]
    [InlineData(HunterSubmissionStatus.Approved)]
    [InlineData(HunterSubmissionStatus.Rejected)]
    public async Task NonDraftStatus_ShouldBlockNewCaptureSession(
        HunterSubmissionStatus status)
    {
        var submission = CreateSubmission(HunterA, status);

        var (handler, resolver, store) =
            CreateHandler(submission);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(submission.Id)));

        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task UnknownStatus_ShouldFailClosed()
    {
        var submission = CreateSubmission(
            HunterA,
            (HunterSubmissionStatus)999);

        var (handler, resolver, store) =
            CreateHandler(submission);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(submission.Id)));

        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task RejectedSubmission_ShouldNotBlockNewDraft()
    {
        var rejected = CreateSubmission(
            HunterA,
            HunterSubmissionStatus.Rejected);

        var newDraft = CreateSubmission(
            HunterA,
            HunterSubmissionStatus.Draft);

        var (handler, resolver, store) =
            CreateHandler(rejected, newDraft);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(rejected.Id)));

        Assert.Equal(0, store.CreateCount);

        var response = await handler.HandleAsync(
            HunterA,
            CreateRequest(newDraft.Id));

        Assert.Equal(newDraft.Id, response.HunterSubmissionId);
        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal(1, store.CreateCount);
    }

    [Fact]
    public async Task OtherHuntersDraft_ShouldBeDenied()
    {
        var submission = CreateSubmission(
            HunterB,
            HunterSubmissionStatus.Draft);

        var (handler, resolver, store) =
            CreateHandler(submission);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(submission.Id)));

        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task MissingSubmission_ShouldBeDenied()
    {
        var (handler, resolver, store) = CreateHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(Guid.NewGuid())));

        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotCreateSession()
    {
        var submission = CreateSubmission(
            HunterA,
            HunterSubmissionStatus.Draft);

        var (handler, resolver, store) =
            CreateHandler(submission);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(submission.Id),
                cts.Token));

        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }
}
