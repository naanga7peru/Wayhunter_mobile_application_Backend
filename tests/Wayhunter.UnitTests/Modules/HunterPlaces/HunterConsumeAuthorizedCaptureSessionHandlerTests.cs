
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterConsumeAuthorizedCaptureSessionHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeIdentityResolver
        : IHunterCaptureSessionIdentityResolver
    {
        public Guid? HunterId { get; set; }

        public int Calls { get; private set; }

        public Task<Guid?> ResolveSessionHunterIdAsync(
            string authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Calls++;

            return Task.FromResult(HunterId);
        }
    }

    private sealed class Fixture
    {
        public required HunterConsumeAuthorizedCaptureSessionHandler
            Handler
        { get; init; }

        public required InMemoryHunterSubmissionStatusTransitionStore
            Store
        { get; init; }

        public required FakeIdentityResolver Resolver { get; init; }

        public required Guid OwnerId { get; init; }

        public required Guid SubmissionId { get; init; }

        public string OwnerUserId => OwnerId.ToString("D");

        public Task<Guid> IssueAsync(
            DateTimeOffset? issuedAt = null,
            DateTimeOffset? expiresAt = null)
        {
            return Store.CreateAsync(
                OwnerId,
                SubmissionId,
                issuedAt ?? Now.AddMinutes(-1),
                expiresAt ?? Now.AddMinutes(9));
        }

        public Task<bool> ConsumeAsync(Guid sessionId)
        {
            return Handler.HandleAsync(
                OwnerUserId,
                sessionId,
                SubmissionId);
        }
    }

    private static Fixture CreateFixture(
        HunterSubmissionStatus status =
            HunterSubmissionStatus.Draft)
    {
        var ownerId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var submission = new HunterSubmission
        {
            Id = submissionId,
            UserId = ownerId.ToString("D"),
            PlaceName = "Hunter Test Place",
            District = "Jaffna",
            Status = status
        };

        var store =
            new InMemoryHunterSubmissionStatusTransitionStore(
                new[] { submission });

        var resolver = new FakeIdentityResolver
        {
            HunterId = ownerId
        };

        var ownershipService =
            new HunterCaptureSubmissionOwnershipService(store);

        var identityService =
            new HunterCaptureSessionIdentityResolutionService(
                resolver);

        var sessionService = new HunterCaptureSessionService(
            store,
            new FixedUtcTimeProvider());

        var handler =
            new HunterConsumeAuthorizedCaptureSessionHandler(
                ownershipService,
                identityService,
                sessionService);

        return new Fixture
        {
            Handler = handler,
            Store = store,
            Resolver = resolver,
            OwnerId = ownerId,
            SubmissionId = submissionId
        };
    }

    [Fact]
    public async Task ValidDraftOwner_ShouldConsumeSession()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        var result = await fixture.ConsumeAsync(sessionId);

        Assert.True(result);
        Assert.Equal(1, fixture.Resolver.Calls);
    }

    [Fact]
    public async Task RepeatedConsumption_ShouldReturnFalse()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        Assert.True(await fixture.ConsumeAsync(sessionId));
        Assert.False(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task ForeignOwner_ShouldBeDenied()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        var foreignUserId = Guid.NewGuid().ToString("D");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.Handler.HandleAsync(
                foreignUserId,
                sessionId,
                fixture.SubmissionId));

        Assert.Equal(0, fixture.Resolver.Calls);

        // Denied access must not consume the real owner's session.
        Assert.True(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task MissingSubmission_ShouldBeDenied()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.Handler.HandleAsync(
                fixture.OwnerUserId,
                sessionId,
                Guid.NewGuid()));

        Assert.Equal(0, fixture.Resolver.Calls);

        Assert.True(await fixture.ConsumeAsync(sessionId));
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.PendingReview)]
    [InlineData(HunterSubmissionStatus.Held)]
    [InlineData(HunterSubmissionStatus.Approved)]
    [InlineData(HunterSubmissionStatus.Rejected)]
    public async Task NonDraftSubmission_ShouldBeDenied(
        HunterSubmissionStatus status)
    {
        var fixture = CreateFixture(status);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.Handler.HandleAsync(
                fixture.OwnerUserId,
                Guid.NewGuid(),
                fixture.SubmissionId));

        Assert.Equal(0, fixture.Resolver.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingAuthenticatedIdentity_ShouldBeDenied(
        string? userId)
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.Handler.HandleAsync(
                userId,
                Guid.NewGuid(),
                fixture.SubmissionId));

        Assert.Equal(0, fixture.Resolver.Calls);
    }

    [Fact]
    public async Task EmptySessionId_ShouldBeRejected()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Handler.HandleAsync(
                fixture.OwnerUserId,
                Guid.Empty,
                fixture.SubmissionId));

        Assert.Equal(0, fixture.Resolver.Calls);
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldBeRejected()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Handler.HandleAsync(
                fixture.OwnerUserId,
                Guid.NewGuid(),
                Guid.Empty));

        Assert.Equal(0, fixture.Resolver.Calls);
    }

    [Fact]
    public async Task MissingIdentityMapping_ShouldBeDenied()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        fixture.Resolver.HunterId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.ConsumeAsync(sessionId));

        // The rejected operation must not consume the session.
        fixture.Resolver.HunterId = fixture.OwnerId;

        Assert.True(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task EmptyIdentityMapping_ShouldBeDenied()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        fixture.Resolver.HunterId = Guid.Empty;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.ConsumeAsync(sessionId));

        fixture.Resolver.HunterId = fixture.OwnerId;

        Assert.True(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task IncorrectMappedHunter_ShouldReturnFalse()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        fixture.Resolver.HunterId = Guid.NewGuid();

        Assert.False(await fixture.ConsumeAsync(sessionId));

        fixture.Resolver.HunterId = fixture.OwnerId;

        Assert.True(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task RevokedSession_ShouldReturnFalse()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        var revoked = await fixture.Store.RevokeByHunterIdAsync(
            fixture.OwnerId);

        Assert.Equal(1, revoked);
        Assert.False(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task ExpiredSession_ShouldReturnFalse()
    {
        var fixture = CreateFixture();

        var sessionId = await fixture.IssueAsync(
            Now.AddMinutes(-15),
            Now.AddMinutes(-1));

        Assert.False(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task FutureSession_ShouldReturnFalse()
    {
        var fixture = CreateFixture();

        var sessionId = await fixture.IssueAsync(
            Now.AddMinutes(1),
            Now.AddMinutes(11));

        Assert.False(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task StatusTransition_ShouldPreventConsumption()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        var transitioned =
            await fixture.Store.TryTransitionAndRevokeCaptureSessionsAsync(
                fixture.SubmissionId,
                HunterSubmissionStatus.Draft,
                HunterSubmissionStatus.PendingReview);

        Assert.True(transitioned);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public async Task ConcurrentConsumption_ShouldAllowOnlyOne()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        var attempts = Enumerable.Range(0, 10)
            .Select(_ => fixture.ConsumeAsync(sessionId));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(9, results.Count(result => !result));
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotConsumeSession()
    {
        var fixture = CreateFixture();
        var sessionId = await fixture.IssueAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Handler.HandleAsync(
                fixture.OwnerUserId,
                sessionId,
                fixture.SubmissionId,
                cts.Token));

        Assert.Equal(0, fixture.Resolver.Calls);

        Assert.True(await fixture.ConsumeAsync(sessionId));
    }

    [Fact]
    public void NullDependencies_ShouldThrow()
    {
        var fixture = CreateFixture();

        var ownershipService =
            new HunterCaptureSubmissionOwnershipService(
                fixture.Store);

        var identityService =
            new HunterCaptureSessionIdentityResolutionService(
                fixture.Resolver);

        var sessionService =
            new HunterCaptureSessionService(
                fixture.Store,
                new FixedUtcTimeProvider());

        Assert.Throws<ArgumentNullException>(
            () => new HunterConsumeAuthorizedCaptureSessionHandler(
                null!,
                identityService,
                sessionService));

        Assert.Throws<ArgumentNullException>(
            () => new HunterConsumeAuthorizedCaptureSessionHandler(
                ownershipService,
                null!,
                sessionService));

        Assert.Throws<ArgumentNullException>(
            () => new HunterConsumeAuthorizedCaptureSessionHandler(
                ownershipService,
                identityService,
                null!));
    }
}
