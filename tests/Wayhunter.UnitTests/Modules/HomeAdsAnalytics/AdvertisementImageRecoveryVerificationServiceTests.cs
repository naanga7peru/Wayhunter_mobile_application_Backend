using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageRecoveryVerificationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static AdvertisementImageUploadOperation Operation(
        AdvertisementImageRecoveryState state,
        int retries = 0) => new()
    {
        UploadOperationId = Guid.NewGuid(),
        AdvertisementId = Guid.NewGuid(),
        AdministratorId = "trusted-admin",
        PreviousImageUrl = "https://example.com/old.png",
        ProviderAssetId = "wayhunter/advertisements/test-asset",
        ReplacementImageUrl = "https://example.com/new.png",
        State = state,
        CreatedAtUtc = Now.AddHours(-2),
        UpdatedAtUtc = Now.AddMinutes(-5),
        Revision = 1,
        RetryCount = retries
    };

    [Fact]
    public async Task UnknownSqlOutcome_SchedulesRetryWithoutCleanup()
    {
        var repository = new FakeRepository
        {
            Current = Operation(
                AdvertisementImageRecoveryState.SqlOutcomeUnknown)
        };

        var verifier = new FakeVerifier
        {
            Decision = AdvertisementAssetReferenceDecision.SafeForCleanup
        };

        var service = new AdvertisementImageRecoveryVerificationService(
            repository, verifier);

        var result = await service.VerifyAndScheduleAsync(
            repository.Current.UploadOperationId,
            Guid.NewGuid(),
            Now,
            TimeSpan.FromMinutes(5),
            3,
            TimeSpan.FromMinutes(10));

        Assert.True(result);
        Assert.Equal(1, verifier.Calls);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(1, repository.Current.RetryCount);
        Assert.Equal(3, repository.Current.Revision);
        Assert.Equal(
            AdvertisementImageRecoveryState.SqlOutcomeUnknown,
            repository.Current.State);
    }

    [Fact]
    public async Task ClaimRejection_DoesNotVerifyOrSave()
    {
        var repository = new FakeRepository
        {
            Current = Operation(
                AdvertisementImageRecoveryState.SqlOutcomeUnknown),
            RejectClaim = true
        };

        var verifier = new FakeVerifier();
        var service = new AdvertisementImageRecoveryVerificationService(
            repository, verifier);

        var result = await service.VerifyAndScheduleAsync(
            repository.Current.UploadOperationId,
            Guid.NewGuid(),
            Now,
            TimeSpan.FromMinutes(5),
            3,
            TimeSpan.FromMinutes(10));

        Assert.False(result);
        Assert.Equal(0, verifier.Calls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task RetryExhaustion_RequiresManualReview()
    {
        var repository = new FakeRepository
        {
            Current = Operation(
                AdvertisementImageRecoveryState.UploadOutcomeUnknown,
                retries: 2)
        };

        var verifier = new FakeVerifier();
        var service = new AdvertisementImageRecoveryVerificationService(
            repository, verifier);

        var result = await service.VerifyAndScheduleAsync(
            repository.Current.UploadOperationId,
            Guid.NewGuid(),
            Now,
            TimeSpan.FromMinutes(5),
            3,
            TimeSpan.FromMinutes(10));

        Assert.True(result);
        Assert.Equal(
            AdvertisementImageRecoveryState.ManualReview,
            repository.Current.State);
        Assert.Equal(3, repository.Current.RetryCount);
        Assert.Null(repository.Current.NextRetryAtUtc);
        Assert.Equal(0, verifier.Calls);
    }

    [Fact]
    public async Task VerificationFailure_DoesNotSaveOutcome()
    {
        var repository = new FakeRepository
        {
            Current = Operation(
                AdvertisementImageRecoveryState.SqlOutcomeUnknown)
        };

        var verifier = new FakeVerifier { ThrowOnVerify = true };
        var service = new AdvertisementImageRecoveryVerificationService(
            repository, verifier);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VerifyAndScheduleAsync(
                repository.Current.UploadOperationId,
                Guid.NewGuid(),
                Now,
                TimeSpan.FromMinutes(5),
                3,
                TimeSpan.FromMinutes(10)));

        Assert.Equal(0, repository.SaveCalls);
    }

    private sealed class FakeVerifier
        : IAdvertisementImageReferenceVerifier
    {
        public int Calls { get; private set; }
        public bool ThrowOnVerify { get; set; }

        public AdvertisementAssetReferenceDecision Decision { get; set; } =
            AdvertisementAssetReferenceDecision.Unknown;

        public Task<AdvertisementAssetReferenceResult> VerifyAsync(
            Guid advertisementId,
            Guid uploadOperationId,
            string providerAssetId,
            string replacementImageUrl,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            if (ThrowOnVerify)
                throw new InvalidOperationException("Verifier unavailable.");

            return Task.FromResult(
                new AdvertisementAssetReferenceResult(
                    Decision,
                    "TEST_ONLY"));
        }
    }

    private sealed class FakeRepository
        : IAdvertisementImageRecoveryRepository
    {
        public required AdvertisementImageUploadOperation Current
        {
            get;
            set;
        }

        public bool RejectClaim { get; set; }
        public int SaveCalls { get; private set; }

        public Task<bool> TryCreateAsync(
            AdvertisementImageUploadOperation operation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<AdvertisementImageUploadOperation?> GetAsync(
            Guid uploadOperationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdvertisementImageUploadOperation?>(
                Current.UploadOperationId == uploadOperationId
                    ? Current
                    : null);

        public Task<AdvertisementImageUploadOperation?> TryClaimAsync(
            Guid uploadOperationId,
            Guid claimToken,
            DateTimeOffset nowUtc,
            DateTimeOffset leaseExpiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            if (RejectClaim ||
                Current.UploadOperationId != uploadOperationId)
            {
                return Task.FromResult<AdvertisementImageUploadOperation?>(
                    null);
            }

            Current = Current with
            {
                ClaimToken = claimToken,
                LeaseExpiresAtUtc = leaseExpiresAtUtc,
                Revision = Current.Revision + 1
            };

            return Task.FromResult<AdvertisementImageUploadOperation?>(
                Current);
        }

        public Task<bool> TrySaveAsync(
            AdvertisementImageUploadOperation operation,
            long expectedRevision,
            Guid expectedClaimToken,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;

            if (Current.Revision != expectedRevision ||
                Current.ClaimToken != expectedClaimToken ||
                operation.Revision != expectedRevision + 1)
            {
                return Task.FromResult(false);
            }

            Current = operation;
            return Task.FromResult(true);
        }
    }
}