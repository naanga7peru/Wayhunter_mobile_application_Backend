using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterReviewServiceTests
{
    private sealed class Store : IHunterReviewStore
    {
        public HunterReviewDecision? Decision { get; private set; }
        public bool ReturnConflict { get; set; }

        public Task<HunterSubmissionResponseDto?> TryReviewPendingAsync(
            HunterReviewDecision decision,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Decision = decision;
            return Task.FromResult<HunterSubmissionResponseDto?>(
                ReturnConflict
                    ? null
                    : new HunterSubmissionResponseDto
                    {
                        SubmissionId = decision.HunterSubmissionId,
                        Status = decision.ResultingStatus,
                        SubmittedAtUtc = decision.ReviewedAtUtc.AddMinutes(-1),
                        CreatedAtUtc = decision.ReviewedAtUtc.AddMinutes(-2)
                    });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task ReviewPersistsTrustedActorAndServerTime()
    {
        var now = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new Store();
        var service = new HunterReviewService(
            store, new FixedTimeProvider(now));
        var submissionId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = submissionId,
            ResultingStatus = HunterSubmissionStatus.Approved
        };

        var response = await service.ReviewAsync(
            reviewerId.ToString("D"), request);

        Assert.Equal(submissionId, response.SubmissionId);
        Assert.Equal(HunterSubmissionStatus.Approved, response.Status);
        Assert.NotNull(store.Decision);
        Assert.Equal(reviewerId.ToString("D"), store.Decision.ReviewerUserId);
        Assert.Equal(now, store.Decision.ReviewedAtUtc);
        Assert.Equal(HunterSubmissionStatus.Approved, store.Decision.ResultingStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task MissingOrMalformedAdminIdentityIsRejected(string? actor)
    {
        var store = new Store();
        var service = new HunterReviewService(store, TimeProvider.System);
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = Guid.NewGuid(),
            ResultingStatus = HunterSubmissionStatus.Approved
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ReviewAsync(actor!, request));

        Assert.Null(store.Decision);
    }

    [Fact]
    public async Task InvalidReviewAndMissingReasonDoNotReachStore()
    {
        var store = new Store();
        var service = new HunterReviewService(store, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(
                Guid.NewGuid().ToString("D"),
                new HunterReviewRequestDto
                {
                    HunterSubmissionId = Guid.Empty,
                    ResultingStatus = HunterSubmissionStatus.Approved
                }));

        Assert.Null(store.Decision);
    }

    [Fact]
    public async Task DuplicateOrStaleReviewIsReportedAsConflict()
    {
        var store = new Store { ReturnConflict = true };
        var service = new HunterReviewService(store, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReviewAsync(
                Guid.NewGuid().ToString("D"),
                new HunterReviewRequestDto
                {
                    HunterSubmissionId = Guid.NewGuid(),
                    ResultingStatus = HunterSubmissionStatus.Approved
                }));
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.Rejected)]
    [InlineData(HunterSubmissionStatus.Held)]
    public async Task UnapprovedDecisionSemanticsAreNotApplied(
        HunterSubmissionStatus status)
    {
        var store = new Store();
        var service = new HunterReviewService(store, TimeProvider.System);
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = Guid.NewGuid(),
            ResultingStatus = status,
            Reason = "Owner decision pending"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReviewAsync(Guid.NewGuid().ToString("D"), request));
        Assert.Null(store.Decision);
    }

    [Fact]
    public async Task OversizedReasonIsRejected()
    {
        var store = new Store();
        var service = new HunterReviewService(store, TimeProvider.System);
        var request = new HunterReviewRequestDto
        {
            HunterSubmissionId = Guid.NewGuid(),
            ResultingStatus = HunterSubmissionStatus.Approved,
            Reason = new string('x', 2001)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(Guid.NewGuid().ToString("D"), request));
        Assert.Null(store.Decision);
    }
}
