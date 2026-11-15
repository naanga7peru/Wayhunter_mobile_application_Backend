
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterStartCaptureSessionHandlerTests
{
    private sealed class FakeCaptureSessionStore
        : IHunterCaptureSessionStore
    {
        public Guid SessionIdToReturn { get; set; } =
            Guid.NewGuid();

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
        HunterStartCaptureSessionHandler Handler,
        FakeCaptureSessionStore Store)
        CreateHandler()
    {
        var store = new FakeCaptureSessionStore();

        var service = new HunterCaptureSessionService(
            store,
            TimeProvider.System);

        var handler = new HunterStartCaptureSessionHandler(
            service);

        return (handler, store);
    }

    [Fact]
    public void NullService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HunterStartCaptureSessionHandler(null!));
    }

    [Fact]
    public async Task ValidRequest_ShouldCreateSession()
    {
        var (handler, store) = CreateHandler();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var request = new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = submissionId
        };

        var response = await handler.HandleAsync(
            hunterId,
            request);

        Assert.Equal(
            store.SessionIdToReturn,
            response.CaptureSessionId);

        Assert.Equal(
            submissionId,
            response.HunterSubmissionId);

        Assert.Equal(1, store.CreateCount);
        Assert.Equal(hunterId, store.LastHunterId);
        Assert.Equal(submissionId, store.LastSubmissionId);
    }

    [Fact]
    public async Task EmptyHunterId_ShouldThrow()
    {
        var (handler, store) = CreateHandler();

        var request = new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(Guid.Empty, request));

        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldThrow()
    {
        var (handler, store) = CreateHandler();

        var request = new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = Guid.Empty
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(Guid.NewGuid(), request));

        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task NullRequest_ShouldThrow()
    {
        var (handler, store) = CreateHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.HandleAsync(Guid.NewGuid(), null!));

        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotCreateSession()
    {
        var (handler, store) = CreateHandler();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = Guid.NewGuid()
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                Guid.NewGuid(),
                request,
                cts.Token));

        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task EmptySessionIdFromStore_ShouldThrow()
    {
        var (handler, store) = CreateHandler();

        store.SessionIdToReturn = Guid.Empty;

        var request = new HunterStartCaptureSessionRequestDto
        {
            HunterSubmissionId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(Guid.NewGuid(), request));

        Assert.Equal(1, store.CreateCount);
    }
}
