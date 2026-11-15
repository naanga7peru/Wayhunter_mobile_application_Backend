
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterReaderAuthorizationIntegrationTests
{
    private const string HunterA = "hunter-001";
    private const string HunterB = "hunter-002";

    private sealed class FakeIdentityResolver
        : IHunterCaptureSessionIdentityResolver
    {
        public Guid? HunterId { get; set; } = Guid.NewGuid();

        public int CallCount { get; private set; }

        public string? LastUserId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<Guid?> ResolveSessionHunterIdAsync(
            string authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUserId = authenticatedUserId;
            LastToken = cancellationToken;

            return Task.FromResult(HunterId);
        }
    }

    private sealed class FakeSessionStore
        : IHunterCaptureSessionStore
    {
        public int CreateCount { get; private set; }

        public Guid SessionId { get; } = Guid.NewGuid();

        public Guid LastHunterId { get; private set; }

        public Guid LastSubmissionId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<Guid> CreateAsync(
            Guid hunterId,
            Guid hunterSubmissionId,
            DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            LastHunterId = hunterId;
            LastSubmissionId = hunterSubmissionId;
            LastToken = cancellationToken;

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
        Guid? submissionId = null)
    {
        return new HunterSubmission
        {
            Id = submissionId ?? Guid.NewGuid(),
            UserId = ownerUserId,
            PlaceName = "Test Hunter Place",
            District = "Jaffna"
        };
    }

    private static HunterStartCaptureSessionRequestDto CreateRequest(
        Guid submissionId)
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
        // Real in-memory submission reader.
        var reader =
            new InMemoryHunterCaptureSubmissionAccessReader(
                submissions);

        // Real ownership service.
        var ownershipService =
            new HunterCaptureSubmissionOwnershipService(reader);

        // Fake resolver: no unapproved ID mapping.
        var resolver = new FakeIdentityResolver();

        var identityService =
            new HunterCaptureSessionIdentityResolutionService(
                resolver);

        // Fake storage lets tests verify creation count.
        var store = new FakeSessionStore();

        var sessionService =
            new HunterCaptureSessionService(
                store,
                TimeProvider.System);

        var captureHandler =
            new HunterStartCaptureSessionHandler(
                sessionService);

        // Real authorization orchestration.
        var handler =
            new HunterStartAuthorizedCaptureSessionHandler(
                ownershipService,
                identityService,
                captureHandler);

        return (handler, resolver, store);
    }

    [Fact]
    public async Task CorrectOwner_ShouldCreateSession()
    {
        var submission = CreateSubmission(HunterA);

        var (handler, resolver, store) =
            CreateHandler(submission);

        var result = await handler.HandleAsync(
            HunterA,
            CreateRequest(submission.Id));

        Assert.Equal(store.SessionId, result.CaptureSessionId);
        Assert.Equal(submission.Id, result.HunterSubmissionId);

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(1, store.CreateCount);

        Assert.Equal(HunterA, resolver.LastUserId);
        Assert.Equal(resolver.HunterId!.Value, store.LastHunterId);
        Assert.Equal(submission.Id, store.LastSubmissionId);
    }

    [Fact]
    public async Task DifferentOwner_ShouldNotCreateSession()
    {
        var submission = CreateSubmission(HunterA);

        var (handler, resolver, store) =
            CreateHandler(submission);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterB,
                CreateRequest(submission.Id)));

        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task MissingSubmission_ShouldNotCreateSession()
    {
        var (handler, resolver, store) = CreateHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(Guid.NewGuid())));

        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task MultipleSubmissions_ShouldRespectOwnership()
    {
        var first = CreateSubmission(HunterA);
        var second = CreateSubmission(HunterB);

        var (handler, resolver, store) =
            CreateHandler(first, second);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterB,
                CreateRequest(first.Id)));

        Assert.Equal(0, store.CreateCount);

        var response = await handler.HandleAsync(
            HunterB,
            CreateRequest(second.Id));

        Assert.Equal(second.Id, response.HunterSubmissionId);
        Assert.Equal(HunterB, resolver.LastUserId);
        Assert.Equal(1, store.CreateCount);
    }

    [Fact]
    public async Task MissingIdentityMapping_ShouldNotCreateSession()
    {
        var submission = CreateSubmission(HunterA);

        var (handler, resolver, store) =
            CreateHandler(submission);

        resolver.HunterId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(submission.Id)));

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task EmptyIdentityMapping_ShouldNotCreateSession()
    {
        var submission = CreateSubmission(HunterA);

        var (handler, resolver, store) =
            CreateHandler(submission);

        resolver.HunterId = Guid.Empty;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(submission.Id)));

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingAuthenticatedUser_ShouldDeny(
        string? userId)
    {
        var submission = CreateSubmission(HunterA);

        var (handler, resolver, store) =
            CreateHandler(submission);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                userId,
                CreateRequest(submission.Id)));

        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldReject()
    {
        var submission = CreateSubmission(HunterA);

        var (handler, resolver, store) =
            CreateHandler(submission);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(Guid.Empty)));

        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotCreateSession()
    {
        var submission = CreateSubmission(HunterA);

        var (handler, resolver, store) =
            CreateHandler(submission);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                HunterA,
                CreateRequest(submission.Id),
                cts.Token));

        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task CancellationToken_ShouldBeForwarded()
    {
        var submission = CreateSubmission(HunterA);

        var (handler, resolver, store) =
            CreateHandler(submission);

        using var cts = new CancellationTokenSource();

        await handler.HandleAsync(
            HunterA,
            CreateRequest(submission.Id),
            cts.Token);

        Assert.Equal(cts.Token, resolver.LastToken);
        Assert.Equal(cts.Token, store.LastToken);
        Assert.Equal(1, store.CreateCount);
    }
}
