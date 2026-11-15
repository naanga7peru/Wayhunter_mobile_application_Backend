
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterStartAuthorizedCaptureSessionHandlerTests
{
    private const string UserId =
        "11111111-1111-4111-8111-111111111111";

    private const string OtherUserId =
        "22222222-2222-4222-8222-222222222222";

    private sealed class FakeAccessReader
        : IHunterCaptureSubmissionAccessReader
    {
        public HunterCaptureSubmissionAccessSnapshot? Snapshot
        {
            get;
            set;
        }

        public int ReadCount { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<HunterCaptureSubmissionAccessSnapshot?>
            FindBySubmissionIdAsync(
                Guid submissionId,
                CancellationToken cancellationToken = default)
        {
            ReadCount++;
            LastToken = cancellationToken;

            return Task.FromResult(Snapshot);
        }
    }

    private sealed class FakeIdentityResolver
        : IHunterCaptureSessionIdentityResolver
    {
        public Guid? ResolvedHunterId { get; set; }
            = Guid.NewGuid();

        public int ResolveCount { get; private set; }

        public string? LastUserId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<Guid?> ResolveSessionHunterIdAsync(
            string authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            ResolveCount++;
            LastUserId = authenticatedUserId;
            LastToken = cancellationToken;

            return Task.FromResult(ResolvedHunterId);
        }
    }

    private sealed class FakeCaptureSessionStore
        : IHunterCaptureSessionStore
    {
        public Guid SessionIdToReturn { get; set; }
            = Guid.NewGuid();

        public int CreateCount { get; private set; }

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

            return Task.FromResult(SessionIdToReturn);
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

    private static (
        HunterStartAuthorizedCaptureSessionHandler Handler,
        FakeAccessReader Reader,
        FakeIdentityResolver Resolver,
        FakeCaptureSessionStore Store,
        Guid SubmissionId)
        CreateHandler()
    {
        var submissionId = Guid.NewGuid();

        var reader = new FakeAccessReader
        {
            Snapshot = new HunterCaptureSubmissionAccessSnapshot
            {
                SubmissionId = submissionId,
                OwnerUserId = UserId
            }
        };

        var resolver = new FakeIdentityResolver();

        var store = new FakeCaptureSessionStore();

        var ownershipService =
            new HunterCaptureSubmissionOwnershipService(reader);

        var identityService =
            new HunterCaptureSessionIdentityResolutionService(
                resolver);

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

        return (
            handler,
            reader,
            resolver,
            store,
            submissionId);
    }

    private static HunterStartCaptureSessionRequestDto
        CreateRequest(Guid submissionId)
    {
        return new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = submissionId
        };
    }

    [Fact]
    public async Task CorrectOwnerAndIdentity_ShouldCreateSession()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        var response = await handler.HandleAsync(
            UserId,
            CreateRequest(submissionId));

        Assert.Equal(
            store.SessionIdToReturn,
            response.CaptureSessionId);

        Assert.Equal(
            submissionId,
            response.HunterSubmissionId);

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal(1, store.CreateCount);

        Assert.Equal(
            resolver.ResolvedHunterId!.Value,
            store.LastHunterId);

        Assert.Equal(submissionId, store.LastSubmissionId);
        Assert.Equal(UserId, resolver.LastUserId);
    }

    [Fact]
    public async Task DifferentOwner_ShouldNotCreateSession()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        reader.Snapshot =
            new HunterCaptureSubmissionAccessSnapshot
            {
                SubmissionId = submissionId,
                OwnerUserId = OtherUserId
            };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task MissingSubmission_ShouldNotCreateSession()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        reader.Snapshot = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task MismatchedSubmission_ShouldNotCreateSession()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        reader.Snapshot =
            new HunterCaptureSubmissionAccessSnapshot
            {
                SubmissionId = Guid.NewGuid(),
                OwnerUserId = UserId
            };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task MissingIdentityMapping_ShouldNotCreateSession()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        resolver.ResolvedHunterId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task EmptyGuidMapping_ShouldNotCreateSession()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        resolver.ResolvedHunterId = Guid.Empty;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingAuthenticatedUserId_ShouldDeny(
        string? authenticatedUserId)
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                authenticatedUserId,
                CreateRequest(submissionId)));

        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldReject()
    {
        var (handler, reader, resolver, store, _)
            = CreateHandler();

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(
                UserId,
                CreateRequest(Guid.Empty)));

        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task NullRequest_ShouldReject()
    {
        var (handler, reader, resolver, store, _)
            = CreateHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(UserId, null!));

        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotCreateSession()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                UserId,
                CreateRequest(submissionId),
                cts.Token));

        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(0, resolver.ResolveCount);
        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task CancellationToken_ShouldBeForwarded()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        using var cts = new CancellationTokenSource();

        await handler.HandleAsync(
            UserId,
            CreateRequest(submissionId),
            cts.Token);

        Assert.Equal(cts.Token, reader.LastToken);
        Assert.Equal(cts.Token, resolver.LastToken);
        Assert.Equal(cts.Token, store.LastToken);
        Assert.Equal(1, store.CreateCount);
    }

    [Fact]
    public async Task InvalidSessionResult_ShouldThrow()
    {
        var (handler, reader, resolver, store, submissionId)
            = CreateHandler();

        store.SessionIdToReturn = Guid.Empty;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                UserId,
                CreateRequest(submissionId)));

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(1, resolver.ResolveCount);
        Assert.Equal(1, store.CreateCount);
    }
}
