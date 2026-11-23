using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterDraftServiceTests
{
    private sealed class Store : IHunterDraftStore
    {
        public HunterSubmission? Submission { get; private set; }
        public string? KeyHash { get; private set; }
        public string? Digest { get; private set; }
        public Task<HunterSubmission> CreateAsync(HunterSubmission submission, string keyHash, string requestDigest,
            CancellationToken cancellationToken = default)
        { Submission = submission; KeyHash = keyHash; Digest = requestDigest; return Task.FromResult(submission); }
        public Task<HunterDraftUpdateResult> UpdateDraftAsync(Guid submissionId, string ownerUserId,
            HunterDraftRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult(HunterDraftUpdateResult.Updated);
        public Task<HunterDraftDeleteResult> DeleteDraftAsync(Guid submissionId, string ownerUserId,
            CancellationToken cancellationToken = default) => Task.FromResult(HunterDraftDeleteResult.Deleted);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }

    [Fact]
    public async Task CreateUsesTrustedOwnerSnapshotServerTimeAndHashedIdempotency()
    {
        var now = new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var store = new Store();
        var service = new HunterDraftService(store, new Clock(now));
        var response = await service.CreateAsync(userId.ToString("D"), "Trusted Name", Valid(), "request-key-123");
        Assert.NotEqual(Guid.Empty, response.SubmissionId);
        Assert.Equal("Trusted Name", store.Submission!.UserNameSnapshot);
        Assert.Equal(userId.ToString("D"), store.Submission.UserId);
        Assert.Equal(now, store.Submission.CreatedAtUtc);
        Assert.Equal(64, store.KeyHash!.Length);
        Assert.Equal(64, store.Digest!.Length);
        Assert.DoesNotContain("request-key-123", store.KeyHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyIdempotencyKeyIsRejectedBeforePersistence()
    {
        var store = new Store();
        var service = new HunterDraftService(store, TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(Guid.NewGuid().ToString("D"), "Trusted Name", Valid(), " "));
        Assert.Null(store.Submission);
    }

    private static HunterDraftRequestDto Valid() => new()
    { PlaceName = "Waterfall", ClaimedDistrict = "Jaffna", Latitude = 9.66, Longitude = 80.0 };
}
